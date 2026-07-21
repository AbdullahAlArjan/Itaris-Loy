using Itaris.Infrastructure.Auth;
using Itaris.Infrastructure.Http;
using Itaris.Modules.Loyalty.Features.ManagePrograms;
using Itaris.Modules.Loyalty.Features.Memberships;
using Itaris.Modules.Loyalty.Features.Preview;
using Itaris.Modules.Loyalty.Persistence;
using Itaris.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Itaris.Modules.Loyalty.PublicApi;

/// <summary>
/// Composition entry point for the Loyalty module: programs (points/stamps) with versioned rules,
/// memberships with a cached balance projection, and the pure points-preview calculator.
/// </summary>
public static class LoyaltyModule
{
    public static IServiceCollection AddLoyaltyModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<LoyaltyDbContext>(options =>
            options.UseNpgsql(connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", LoyaltyDbContext.Schema)));

        services.AddScoped<CreateProgramHandler>();
        services.AddScoped<UpdateRulesHandler>();
        services.AddScoped<SetProgramStatusHandler>();
        services.AddScoped<PreviewHandler>();
        services.AddScoped<JoinProgramHandler>();
        services.AddScoped<GetMembershipsHandler>();

        return services;
    }

    /// <summary>Applies pending loyalty-schema migrations. Dev/test startup convenience.</summary>
    public static async Task MigrateLoyaltyAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<LoyaltyDbContext>().Database.MigrateAsync();
    }

    public static IEndpointRouteBuilder MapLoyaltyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        MapProgramEndpoints(endpoints);
        MapPreviewEndpoint(endpoints);
        MapMembershipEndpoints(endpoints);
        return endpoints;
    }

    private static Guid RequireMerchant(ICurrentUser user) =>
        user.MerchantId ?? throw new ApiException(403, ErrorCodes.Forbidden, "No merchant scope on this token.");

    private static Guid RequireCustomer(ICurrentUser user) =>
        user.Id ?? throw new ApiException(401, ErrorCodes.Unauthorized, "Authentication required.");

    private static void MapProgramEndpoints(IEndpointRouteBuilder endpoints)
    {
        var programs = endpoints.MapGroup("/v1/merchant/programs").WithTags("Loyalty programs");

        // doc 05 E1 — create a program
        programs.MapPost("", async (
                CreateProgramRequest request, CreateProgramHandler handler, ICurrentUser user, CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(RequireMerchant(user), request, ct);
                return Results.Ok(result.OrThrow());
            })
            .RequirePermission("programs.manage")
            .Produces<ProgramResponse>()
            .WithSummary("Create a loyalty program (doc 05 E1)");

        // doc 05 E2 — set/replace rules (new version)
        programs.MapPatch("/{programId:guid}/rules", async (
                Guid programId, UpdateRulesRequest request, UpdateRulesHandler handler,
                ICurrentUser user, CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(RequireMerchant(user), programId, request, ct);
                return Results.Ok(result.OrThrow());
            })
            .RequirePermission("programs.manage")
            .Produces<UpdateRulesResponse>()
            .WithSummary("Set program rules, creating a new version (doc 05 E2)");

        // doc 05 E3 — activate / pause
        programs.MapPost("/{programId:guid}/activate", async (
                Guid programId, SetProgramStatusHandler handler, ICurrentUser user, CancellationToken ct) =>
            {
                var result = await handler.ActivateAsync(RequireMerchant(user), programId, ct);
                return Results.Ok(result.OrThrow());
            })
            .RequirePermission("programs.manage")
            .Produces<ProgramResponse>()
            .WithSummary("Activate a program (doc 05 E3)");

        programs.MapPost("/{programId:guid}/pause", async (
                Guid programId, SetProgramStatusHandler handler, ICurrentUser user, CancellationToken ct) =>
            {
                var result = await handler.PauseAsync(RequireMerchant(user), programId, ct);
                return Results.Ok(result.OrThrow());
            })
            .RequirePermission("programs.manage")
            .Produces<ProgramResponse>()
            .WithSummary("Pause a program (doc 05 E3)");
    }

    private static void MapPreviewEndpoint(IEndpointRouteBuilder endpoints)
    {
        // doc 05 E4 — preview earning for an amount (cashier live preview + dashboard)
        endpoints.MapPost("/v1/loyalty/preview", async (
                PreviewRequest request, PreviewHandler handler, ICurrentUser user, CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(RequireMerchant(user), request, ct);
                return Results.Ok(result.OrThrow());
            })
            .RequireAuthorization()
            .WithTags("Loyalty programs")
            .Produces<PreviewResponse>()
            .WithSummary("Preview points/stamps for an amount (doc 05 E4)");
    }

    private static void MapMembershipEndpoints(IEndpointRouteBuilder endpoints)
    {
        var memberships = endpoints.MapGroup("/v1/customers/me/memberships").WithTags("Memberships");

        // doc 05 C3 — join a merchant's active program
        memberships.MapPost("", async (
                JoinProgramRequest request, JoinProgramHandler handler, ICurrentUser user, CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(RequireCustomer(user), request, ct);
                return Results.Ok(result.OrThrow());
            })
            .RequireAuthorization()
            .Produces<MembershipDto>()
            .WithSummary("Join a loyalty program (doc 05 C3)");

        // doc 05 B4 — list my memberships
        memberships.MapGet("", async (GetMembershipsHandler handler, ICurrentUser user, CancellationToken ct) =>
            {
                var response = await handler.ListAsync(RequireCustomer(user), ct);
                return Results.Ok(response);
            })
            .RequireAuthorization()
            .Produces<MembershipListResponse>()
            .WithSummary("List my memberships (doc 05 B4)");

        // doc 05 B5 — membership detail
        memberships.MapGet("/{membershipId:guid}", async (
                Guid membershipId, GetMembershipsHandler handler, ICurrentUser user, CancellationToken ct) =>
            {
                var result = await handler.DetailAsync(RequireCustomer(user), membershipId, ct);
                return Results.Ok(result.OrThrow());
            })
            .RequireAuthorization()
            .Produces<MembershipDto>()
            .WithSummary("Membership detail (doc 05 B5)");
    }
}
