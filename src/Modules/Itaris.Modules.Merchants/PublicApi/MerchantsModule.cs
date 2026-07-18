using Itaris.Infrastructure.Auth;
using Itaris.Infrastructure.Http;
using Itaris.Modules.Merchants.Domain;
using Itaris.Modules.Merchants.Features.Access;
using Itaris.Modules.Merchants.Persistence;
using Itaris.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Itaris.Modules.Merchants.PublicApi;

/// <summary>
/// Composition entry point for the Merchants module. Hosts merchant lifecycle plus the staff/owner
/// login orchestration (which composes Identity's public token/user services with merchant roles).
/// </summary>
public static class MerchantsModule
{
    public static IServiceCollection AddMerchantsModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<MerchantsDbContext>(options =>
            options.UseNpgsql(connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", MerchantsDbContext.Schema)));

        services.AddScoped<MerchantClaimsResolver>();
        services.AddScoped<CreateMerchantHandler>();
        services.AddScoped<OwnerLoginHandler>();
        services.AddScoped<InviteStaffHandler>();
        services.AddScoped<AcceptInviteHandler>();
        services.AddScoped<StaffLoginHandler>();

        return services;
    }

    /// <summary>Applies pending merchants-schema migrations and seeds roles/permissions.</summary>
    public static async Task MigrateAndSeedMerchantsAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MerchantsDbContext>();
        await db.Database.MigrateAsync();
        await MerchantsSeeder.SeedAsync(db);
    }

    public static IEndpointRouteBuilder MapMerchantsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/v1/auth").WithTags("Authentication");

        // doc 05 A6 — owner email/password login (anonymous)
        auth.MapPost("/owner/login", async (
                OwnerLoginRequest request, OwnerLoginHandler handler, CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(request, ct);
                return Results.Ok(result.OrThrow());
            })
            .AllowAnonymous()
            .Produces<AuthTokensResponse>()
            .WithSummary("Owner login (doc 05 A6)");

        // doc 05 A7 — staff PIN login (anonymous)
        auth.MapPost("/staff/login", async (
                StaffLoginRequest request, StaffLoginHandler handler, CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(request, ct);
                return Results.Ok(result.OrThrow());
            })
            .AllowAnonymous()
            .Produces<AuthTokensResponse>()
            .WithSummary("Staff PIN login (doc 05 A7)");

        // doc 05 A8 — accept staff invite (anonymous; invite token is the credential)
        auth.MapPost("/staff/invites/accept", async (
                AcceptInviteRequest request, AcceptInviteHandler handler, CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(request, ct);
                return Results.Ok(result.OrThrow());
            })
            .AllowAnonymous()
            .Produces<AuthTokensResponse>()
            .WithSummary("Accept staff invite and set PIN (doc 05 A8)");

        var admin = endpoints.MapGroup("/v1/admin").WithTags("Admin");

        // doc 05 C10 — platform admin creates a merchant + owner
        admin.MapPost("/merchants", async (
                CreateMerchantRequest request, CreateMerchantHandler handler, CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(request, ct);
                return Results.Ok(result.OrThrow());
            })
            .RequirePermission("admin.merchants.create")
            .Produces<CreateMerchantResponse>()
            .WithSummary("Admin creates a merchant (doc 05 C10)");

        var merchant = endpoints.MapGroup("/v1/merchant").WithTags("Merchant management");

        // doc 05 C7 — owner/admin invites a staff member (permission-gated → 403 FORBIDDEN otherwise)
        merchant.MapPost("/staff", async (
                InviteStaffRequest request, InviteStaffHandler handler, ICurrentUser currentUser, CancellationToken ct) =>
            {
                if (currentUser.MerchantId is not { } merchantId)
                {
                    throw new ApiException(403, ErrorCodes.Forbidden, "No merchant scope on this token.");
                }

                var result = await handler.HandleAsync(merchantId, request, ct);
                return Results.Ok(result.OrThrow());
            })
            .RequirePermission(Permissions.StaffManage)
            .Produces<InviteStaffResponse>()
            .WithSummary("Invite a staff member (doc 05 C7)");

        return endpoints;
    }
}
