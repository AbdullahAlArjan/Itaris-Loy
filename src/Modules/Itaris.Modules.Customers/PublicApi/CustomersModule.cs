using Itaris.Infrastructure.Auth;
using Itaris.Infrastructure.Http;
using Itaris.Modules.Customers.Features.PosCustomers;
using Itaris.Modules.Customers.Features.Profile;
using Itaris.Modules.Customers.Features.QrToken;
using Itaris.Modules.Customers.Persistence;
using Itaris.Modules.Identity.PublicApi;
using Itaris.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Itaris.Modules.Customers.PublicApi;

/// <summary>
/// Composition entry point for the Customers module: customer profiles, the rotating QR token, and
/// the cashier-facing shadow enroll / phone lookup that power the phone-number-only customer flow.
/// </summary>
public static class CustomersModule
{
    public static IServiceCollection AddCustomersModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<CustomersDbContext>(options =>
            options.UseNpgsql(connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", CustomersDbContext.Schema)));

        services.AddScoped<ICustomerDirectory, CustomerDirectory>();
        services.AddScoped<Features.Deletion.DeletionHandler>();
        services.AddScoped<ProfileHandler>();
        services.AddScoped<EnrollHandler>();
        services.AddScoped<LookupHandler>();
        services.AddScoped<QrTokenHandler>();

        return services;
    }

    /// <summary>Applies pending customers-schema migrations. Dev/test startup convenience.</summary>
    public static async Task MigrateCustomersAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<CustomersDbContext>().Database.MigrateAsync();
    }

    public static IEndpointRouteBuilder MapCustomersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var me = endpoints.MapGroup("/v1/customers/me").WithTags("Customer profile");

        // doc 05 B1 — get my profile (creates/claims lazily)
        me.MapGet("", async (ProfileHandler handler, IUserDirectory users, ICurrentUser user, CancellationToken ct) =>
            {
                var userId = RequireCustomer(user);
                var phone = await users.GetPhoneAsync(userId, ct) ?? string.Empty;
                return Results.Ok(await handler.GetOrCreateAsync(userId, phone, ct));
            })
            .RequireAuthorization()
            .Produces<CustomerProfileDto>()
            .WithSummary("Get my profile (doc 05 B1)");

        // doc 05 B2 — update my profile
        me.MapPatch("", async (
                UpdateProfileRequest request, ProfileHandler handler, IUserDirectory users,
                ICurrentUser user, CancellationToken ct) =>
            {
                var userId = RequireCustomer(user);
                var phone = await users.GetPhoneAsync(userId, ct) ?? string.Empty;
                var result = await handler.UpdateAsync(userId, phone, request, ct);
                return Results.Ok(result.OrThrow());
            })
            .RequireAuthorization()
            .Produces<CustomerProfileDto>()
            .WithSummary("Update my profile (doc 05 B2)");

        // doc 05 B3 — rotating QR identity token
        me.MapGet("/qr-token", (QrTokenHandler handler, ICurrentUser user) =>
                Results.Ok(handler.Issue(RequireCustomer(user))))
            .RequireAuthorization()
            .Produces<QrTokenResponse>()
            .WithSummary("Get my rotating QR token (doc 05 B3)");

        // doc 05 B13 — PDPL account deletion (7-day grace)
        me.MapPost("/deletion-request", async (
                Features.Deletion.DeletionHandler handler, ICurrentUser user, CancellationToken ct) =>
                Results.Ok((await handler.RequestAsync(RequireCustomer(user), ct)).OrThrow()))
            .RequireAuthorization()
            .Produces<Features.Deletion.DeletionRequestResponse>()
            .WithSummary("Request account deletion (doc 05 B13, PDPL)");

        me.MapGet("/deletion-request", async (
                Features.Deletion.DeletionHandler handler, ICurrentUser user, CancellationToken ct) =>
                Results.Ok((await handler.GetAsync(RequireCustomer(user), ct)).OrThrow()))
            .RequireAuthorization()
            .Produces<Features.Deletion.DeletionRequestResponse>()
            .WithSummary("Get my deletion-request status (doc 05 B13)");

        me.MapDelete("/deletion-request", async (
                Features.Deletion.DeletionHandler handler, ICurrentUser user, CancellationToken ct) =>
            {
                (await handler.CancelAsync(RequireCustomer(user), ct)).OrThrow();
                return Results.NoContent();
            })
            .RequireAuthorization()
            .WithSummary("Cancel my deletion request within the grace period (doc 05 B13)");

        var pos = endpoints.MapGroup("/v1/pos/customers").WithTags("POS — customer identification");

        // doc 05 D3 — cashier enrolls a phone-only customer
        pos.MapPost("/enroll", async (EnrollRequest request, EnrollHandler handler, CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(request, ct);
                return Results.Ok(result.OrThrow());
            })
            .RequirePermission("customers.identify")
            .Produces<EnrollResponse>()
            .WithSummary("Enroll a phone-only customer (doc 05 D3)");

        // doc 05 D2 — cashier looks up customers by phone (masked)
        pos.MapGet("/lookup", async (string phone, LookupHandler handler, CancellationToken ct) =>
                Results.Ok(await handler.HandleAsync(phone, ct)))
            .RequirePermission("customers.identify")
            .Produces<LookupResponse>()
            .WithSummary("Look up customers by phone, masked (doc 05 D2)");

        return endpoints;
    }

    private static Guid RequireCustomer(ICurrentUser user) =>
        user.Id ?? throw new ApiException(401, ErrorCodes.Unauthorized, "Authentication required.");
}
