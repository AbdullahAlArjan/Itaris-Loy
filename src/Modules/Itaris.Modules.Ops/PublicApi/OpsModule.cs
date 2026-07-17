using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Itaris.Modules.Ops.PublicApi;

/// <summary>
/// Composition entry point for the Ops module. Phase 1 shell — content arrives with
/// this module's phase in the roadmap (doc 06 Part 12). Wired now so later phases are
/// additive, not restructuring.
/// </summary>
public static class OpsModule
{
    public static IServiceCollection AddOpsModule(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapOpsEndpoints(this IEndpointRouteBuilder endpoints) => endpoints;
}
