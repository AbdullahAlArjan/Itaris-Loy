using Itaris.Infrastructure.Auditing;
using Itaris.Modules.Ops.Features;
using Itaris.Modules.Ops.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Itaris.Modules.Ops.PublicApi;

/// <summary>
/// Composition entry point for the Ops module. Owns the append-only audit trail
/// (ops.audit_logs) and provides the <see cref="IAuditSink"/> the audit interceptor writes through.
/// </summary>
public static class OpsModule
{
    public static IServiceCollection AddOpsModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<OpsDbContext>(options =>
            options.UseNpgsql(connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", OpsDbContext.Schema)));

        services.AddScoped<IAuditSink, AuditSink>();

        return services;
    }

    /// <summary>Applies pending ops-schema migrations. Dev/test startup convenience.</summary>
    public static async Task MigrateOpsAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<OpsDbContext>().Database.MigrateAsync();
    }

    public static IEndpointRouteBuilder MapOpsEndpoints(this IEndpointRouteBuilder endpoints) => endpoints;
}
