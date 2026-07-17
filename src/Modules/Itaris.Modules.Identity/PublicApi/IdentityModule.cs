using FluentValidation;
using Itaris.Modules.Identity.Features.RequestOtp;
using Itaris.Modules.Identity.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Itaris.Modules.Identity.PublicApi;

/// <summary>
/// Composition entry point for the Identity module. The Api project calls only this —
/// everything else in the module is internal wiring (enforced by architecture tests).
/// </summary>
public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", IdentityDbContext.Schema)));

        services.AddScoped<RequestOtpHandler>();
        services.AddSingleton<IValidator<RequestOtpRequest>, RequestOtpValidator>();

        return services;
    }

    /// <summary>Applies pending identity-schema migrations. Dev/test startup convenience.</summary>
    public static async Task MigrateIdentityAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
    }

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/v1/auth").WithTags("Authentication");

        // doc 05 A1 — POST /v1/auth/otp/request (anonymous)
        auth.MapPost("/otp/request", async (
                RequestOtpRequest request,
                IValidator<RequestOtpRequest> validator,
                RequestOtpHandler handler,
                CancellationToken cancellationToken) =>
            {
                var validation = await validator.ValidateAsync(request, cancellationToken);
                if (!validation.IsValid)
                {
                    throw new Itaris.SharedKernel.ApiException(
                        400,
                        Itaris.SharedKernel.ErrorCodes.InvalidPhone,
                        validation.Errors[0].ErrorMessage);
                }

                var result = await handler.HandleAsync(request, cancellationToken);
                return Results.Ok(result.Value);
            })
            .AllowAnonymous()
            .Produces<RequestOtpResponse>()
            .WithSummary("Request a login OTP (doc 05 A1)");

        return endpoints;
    }
}
