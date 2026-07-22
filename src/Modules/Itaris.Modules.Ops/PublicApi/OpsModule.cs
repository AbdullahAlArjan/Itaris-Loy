using Itaris.Infrastructure.Auditing;
using Itaris.Infrastructure.Auth;
using Itaris.Modules.Ops.Features;
using Itaris.Modules.Ops.Features.AuditRead;
using Itaris.Modules.Ops.Persistence;
using Itaris.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Itaris.Modules.Ops.PublicApi;

/// <summary>
/// Composition entry point for the Ops module. Owns the append-only audit trail (ops.audit_logs),
/// provides the <see cref="IAuditSink"/> the audit interceptor writes through, and exposes the
/// merchant-scoped audit-log read.
/// </summary>
public static class OpsModule
{
    public static IServiceCollection AddOpsModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<OpsDbContext>(options =>
            options.UseNpgsql(connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", OpsDbContext.Schema)));

        services.AddScoped<IAuditSink, AuditSink>();
        services.AddScoped<AuditReadHandler>();

        return services;
    }

    /// <summary>Applies pending ops-schema migrations. Dev/test startup convenience.</summary>
    public static async Task MigrateOpsAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<OpsDbContext>().Database.MigrateAsync();
    }

    public static IEndpointRouteBuilder MapOpsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // doc 05 G7 — GET /merchant/audit-logs?actor=&action=&cursor=
        endpoints.MapGet("/v1/merchant/audit-logs", async (
                Guid? actor, string? action, AuditReadHandler handler, ICurrentUser user, CancellationToken ct) =>
            {
                var merchantId = user.MerchantId
                    ?? throw new ApiException(403, ErrorCodes.Forbidden, "No merchant scope on this token.");
                return Results.Ok(await handler.ListAsync(merchantId, actor, action, ct));
            })
            .RequirePermission("audit.view")
            .WithTags("Audit")
            .Produces<AuditLogListResponse>()
            .WithSummary("Read the merchant audit trail (doc 05 G7)");

        return endpoints;
    }
}
