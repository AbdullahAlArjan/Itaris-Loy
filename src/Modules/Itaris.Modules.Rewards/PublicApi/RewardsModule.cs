using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Itaris.Modules.Rewards.PublicApi;

/// <summary>
/// Composition entry point for the Rewards module. Phase 1 shell — content arrives with
/// this module's phase in the roadmap (doc 06 Part 12). Wired now so later phases are
/// additive, not restructuring.
/// </summary>
public static class RewardsModule
{
    public static IServiceCollection AddRewardsModule(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapRewardsEndpoints(this IEndpointRouteBuilder endpoints) => endpoints;
}
