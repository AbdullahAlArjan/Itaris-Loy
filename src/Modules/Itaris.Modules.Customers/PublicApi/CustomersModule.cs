using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Itaris.Modules.Customers.PublicApi;

/// <summary>
/// Composition entry point for the Customers module. Phase 1 shell — content arrives with
/// this module's phase in the roadmap (doc 06 Part 12). Wired now so later phases are
/// additive, not restructuring.
/// </summary>
public static class CustomersModule
{
    public static IServiceCollection AddCustomersModule(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapCustomersEndpoints(this IEndpointRouteBuilder endpoints) => endpoints;
}
