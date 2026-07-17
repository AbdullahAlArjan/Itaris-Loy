using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Itaris.Modules.Loyalty.PublicApi;

/// <summary>
/// Composition entry point for the Loyalty module. Phase 1 shell — content arrives with
/// this module's phase in the roadmap (doc 06 Part 12). Wired now so later phases are
/// additive, not restructuring.
/// </summary>
public static class LoyaltyModule
{
    public static IServiceCollection AddLoyaltyModule(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapLoyaltyEndpoints(this IEndpointRouteBuilder endpoints) => endpoints;
}
