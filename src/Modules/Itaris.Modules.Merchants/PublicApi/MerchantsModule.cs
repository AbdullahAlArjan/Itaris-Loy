using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Itaris.Modules.Merchants.PublicApi;

/// <summary>
/// Composition entry point for the Merchants module. Phase 1 shell — content arrives with
/// this module's phase in the roadmap (doc 06 Part 12). Wired now so later phases are
/// additive, not restructuring.
/// </summary>
public static class MerchantsModule
{
    public static IServiceCollection AddMerchantsModule(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapMerchantsEndpoints(this IEndpointRouteBuilder endpoints) => endpoints;
}
