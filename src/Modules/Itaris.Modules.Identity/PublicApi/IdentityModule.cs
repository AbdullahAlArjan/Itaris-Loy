using FluentValidation;
using Itaris.Infrastructure.Http;
using Itaris.Modules.Identity.Features.AdminLogin;
using Itaris.Modules.Identity.Features.Logout;
using Itaris.Modules.Identity.Features.TokenRefresh;
using Itaris.Modules.Identity.Features.RequestOtp;
using Itaris.Modules.Identity.Features.Shared;
using Itaris.Modules.Identity.Features.VerifyOtp;
using Itaris.Modules.Identity.Persistence;
using Itaris.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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

        services.AddScoped<AuthTokenIssuer>();
        services.AddScoped<ITokenIssuer, Features.Shared.TokenIssuer>();
        services.AddScoped<IUserDirectory, Features.Shared.UserDirectory>();
        services.AddScoped<RequestOtpHandler>();
        services.AddScoped<VerifyOtpHandler>();
        services.AddScoped<RefreshTokenHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<AdminLoginHandler>();
        services.AddSingleton<IValidator<RequestOtpRequest>, RequestOtpValidator>();

        return services;
    }

    /// <summary>Applies pending identity-schema migrations and seeds the platform admin.</summary>
    public static async Task MigrateIdentityAsync(this IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();

        var adminEmail = configuration["Admin:Email"];
        var adminPassword = configuration["Admin:Password"];
        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
        {
            var directory = scope.ServiceProvider.GetRequiredService<IUserDirectory>();
            await directory.EnsureAdminAsync(adminEmail, adminPassword, CancellationToken.None);
        }
    }

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/v1/auth").WithTags("Authentication");

        // doc 05 A1 — POST /v1/auth/otp/request (anonymous)
        auth.MapPost("/otp/request", async (
                RequestOtpRequest request,
                IValidator<RequestOtpRequest> validator,
                RequestOtpHandler handler,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                var validation = await validator.ValidateAsync(request, cancellationToken);
                if (!validation.IsValid)
                {
                    throw new ApiException(400, ErrorCodes.InvalidPhone, validation.Errors[0].ErrorMessage);
                }

                var ip = http.Connection.RemoteIpAddress?.ToString();
                var result = await handler.HandleAsync(request, ip, cancellationToken);
                return Results.Ok(result.OrThrow());
            })
            .AllowAnonymous()
            .Produces<RequestOtpResponse>()
            .WithSummary("Request a login OTP (doc 05 A1)");

        // doc 05 A2 — POST /v1/auth/otp/verify (anonymous)
        auth.MapPost("/otp/verify", async (
                VerifyOtpRequest request,
                VerifyOtpHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(request, cancellationToken);
                return Results.Ok(result.OrThrow());
            })
            .AllowAnonymous()
            .Produces<VerifyOtpResponse>()
            .WithSummary("Verify OTP and issue tokens (doc 05 A2)");

        // doc 05 A3 — POST /v1/auth/token/refresh (anonymous; refresh token is the credential)
        auth.MapPost("/token/refresh", async (
                RefreshTokenRequest request,
                RefreshTokenHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(request, cancellationToken);
                return Results.Ok(result.OrThrow());
            })
            .AllowAnonymous()
            .Produces<RefreshTokenResponse>()
            .WithSummary("Rotate refresh token (doc 05 A3)");

        // doc 05 A4 — POST /v1/auth/logout
        auth.MapPost("/logout", async (
                LogoutRequest request,
                LogoutHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(request, cancellationToken);
                result.OrThrow();
                return Results.NoContent();
            })
            .RequireAuthorization()
            .WithSummary("Revoke the current device's session (doc 05 A4)");

        // doc 05 — platform admin login (anonymous)
        auth.MapPost("/admin/login", async (
                AdminLoginRequest request,
                AdminLoginHandler handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.HandleAsync(request, cancellationToken);
                return Results.Ok(result.OrThrow());
            })
            .AllowAnonymous()
            .Produces<AdminLoginResponse>()
            .WithSummary("Platform admin login");

        return endpoints;
    }
}
