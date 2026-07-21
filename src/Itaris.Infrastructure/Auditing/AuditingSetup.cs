using Microsoft.Extensions.DependencyInjection;

namespace Itaris.Infrastructure.Auditing;

public static class AuditingSetup
{
    /// <summary>
    /// Registers the audit EF interceptor. The <see cref="IAuditSink"/> implementation is
    /// contributed by the Ops module. Call once at composition root.
    /// </summary>
    public static IServiceCollection AddItarisAuditing(this IServiceCollection services)
    {
        // Singleton: EF caches interceptor instances, so it must be stateless w.r.t. scope
        // (it reads the request principal live from IHttpContextAccessor).
        services.AddSingleton<AuditSaveChangesInterceptor>();
        return services;
    }
}
