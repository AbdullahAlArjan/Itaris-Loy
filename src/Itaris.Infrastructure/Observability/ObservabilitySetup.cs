using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

namespace Itaris.Infrastructure.Observability;

/// <summary>
/// Serilog + OpenTelemetry baseline (doc 04: structured logging and OTel traces/metrics
/// from Phase 1; exporters stay console/OTLP-default until a backend is chosen).
/// </summary>
public static class ObservabilitySetup
{
    public const string ServiceName = "itaris-api";

    public static LoggerConfiguration ConfigureItarisSerilog(this LoggerConfiguration configuration) =>
        configuration
            .Enrich.FromLogContext()
            .Enrich.WithProperty("service", ServiceName)
            .WriteTo.Console();

    public static IServiceCollection AddItarisOpenTelemetry(this IServiceCollection services)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation());

        return services;
    }
}
