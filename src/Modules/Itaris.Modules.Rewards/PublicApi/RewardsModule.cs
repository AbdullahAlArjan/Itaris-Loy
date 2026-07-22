using Itaris.Infrastructure.Auth;
using Itaris.Infrastructure.Http;
using Itaris.Modules.Rewards.Features.ManageRewards;
using Itaris.Modules.Rewards.Features.Redeem;
using Itaris.Modules.Rewards.Persistence;
using Itaris.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Itaris.Modules.Rewards.PublicApi;

/// <summary>
/// Composition entry point for the Rewards module: reward catalog with stock, eligibility, and
/// two-phase redemption (intent → confirm) with the double-redemption defense. The [IDEM] redemption
/// endpoints get the shared idempotency filter from the Transactions module.
/// </summary>
public static class RewardsModule
{
    public static IServiceCollection AddRewardsModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<RewardsDbContext>(options =>
            options.UseNpgsql(connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", RewardsDbContext.Schema)));

        services.AddScoped<ManageRewardsHandler>();
        services.AddScoped<EligibilityHandler>();
        services.AddScoped<RedemptionReleaser>();
        services.AddScoped<RedemptionIntentHandler>();
        services.AddScoped<ConfirmRedemptionHandler>();
        services.AddScoped<CancelRedemptionHandler>();
        services.AddScoped<OneStepRedemptionHandler>();
        services.AddScoped<RedemptionReadsHandler>();
        services.AddScoped<ExpireStaleIntentsService>();
        services.AddHostedService<ExpireStaleIntentsWorker>();

        return services;
    }

    /// <summary>Applies pending rewards-schema migrations. Dev/test startup convenience.</summary>
    public static async Task MigrateRewardsAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<RewardsDbContext>().Database.MigrateAsync();
    }

    public static IEndpointRouteBuilder MapRewardsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        MapMerchantRewardEndpoints(endpoints);
        MapCustomerEndpoints(endpoints);
        MapPosEndpoints(endpoints);
        return endpoints;
    }

    private static void MapMerchantRewardEndpoints(IEndpointRouteBuilder endpoints)
    {
        var rewards = endpoints.MapGroup("/v1/merchant/rewards").WithTags("Rewards management");

        rewards.MapPost("", async (CreateRewardRequest request, ManageRewardsHandler handler, ICurrentUser user, CancellationToken ct) =>
                Results.Ok((await handler.CreateAsync(RequireMerchant(user), request, ct)).OrThrow()))
            .RequirePermission("rewards.manage").Produces<RewardDto>().WithSummary("Create a reward (doc 05 F1)");

        rewards.MapGet("", async (ManageRewardsHandler handler, ICurrentUser user, CancellationToken ct) =>
                Results.Ok(await handler.ListAsync(RequireMerchant(user), ct)))
            .RequirePermission("rewards.manage").Produces<RewardListResponse>().WithSummary("List rewards (doc 05 F1)");

        rewards.MapPatch("/{rewardId:guid}", async (Guid rewardId, UpdateRewardRequest request, ManageRewardsHandler handler, ICurrentUser user, CancellationToken ct) =>
                Results.Ok((await handler.UpdateAsync(RequireMerchant(user), rewardId, request, ct)).OrThrow()))
            .RequirePermission("rewards.manage").Produces<RewardDto>().WithSummary("Update a reward (doc 05 F1)");

        rewards.MapPost("/{rewardId:guid}/activate", async (Guid rewardId, ManageRewardsHandler handler, ICurrentUser user, CancellationToken ct) =>
                Results.Ok((await handler.ActivateAsync(RequireMerchant(user), rewardId, ct)).OrThrow()))
            .RequirePermission("rewards.manage").Produces<RewardDto>().WithSummary("Activate a reward (doc 05 F1)");

        rewards.MapPost("/{rewardId:guid}/deactivate", async (Guid rewardId, ManageRewardsHandler handler, ICurrentUser user, CancellationToken ct) =>
                Results.Ok((await handler.DeactivateAsync(RequireMerchant(user), rewardId, ct)).OrThrow()))
            .RequirePermission("rewards.manage").Produces<RewardDto>().WithSummary("Deactivate a reward (doc 05 F1)");

        endpoints.MapGet("/v1/merchant/redemptions", async (Guid? rewardId, RedemptionReadsHandler handler, ICurrentUser user, CancellationToken ct) =>
                Results.Ok(await handler.ListForMerchantAsync(RequireMerchant(user), rewardId, ct)))
            .RequirePermission("rewards.manage").WithTags("Rewards management")
            .Produces<RedemptionListResponse>().WithSummary("List redemptions (doc 05 F2)");
    }

    private static void MapCustomerEndpoints(IEndpointRouteBuilder endpoints)
    {
        // doc 05 B7 — my eligible rewards for a merchant
        endpoints.MapGet("/v1/customers/me/rewards", async (Guid merchantId, EligibilityHandler handler, ICurrentUser user, CancellationToken ct) =>
                Results.Ok(await handler.ForCustomerAsync(merchantId, RequireCustomer(user), ct)))
            .RequireAuthorization().WithTags("Rewards")
            .Produces<EligibilityResponse>().WithSummary("My eligible rewards (doc 05 B7)");

        var redemptions = endpoints.MapGroup("/v1/customers/me/redemptions").WithTags("Redemptions");

        // doc 05 B9 [IDEM] — create redemption intent
        redemptions.MapPost("", async (Guid merchantId, CreateIntentRequest request, RedemptionIntentHandler handler, ICurrentUser user, CancellationToken ct) =>
                (await handler.HandleAsync(merchantId, RequireCustomer(user), request.RewardId, null, ct)).OrThrow())
            .RequireAuthorization().AddEndpointFilter<Transactions.PublicApi.IdempotencyEndpointFilter>()
            .Produces<IntentResponse>().WithSummary("Create redemption intent (doc 05 B9)");

        // doc 05 B8 — my redemption history
        redemptions.MapGet("", async (RedemptionReadsHandler handler, ICurrentUser user, CancellationToken ct) =>
                Results.Ok(await handler.ListForCustomerAsync(RequireCustomer(user), ct)))
            .RequireAuthorization().Produces<RedemptionListResponse>().WithSummary("My redemptions (doc 05 B8)");

        // doc 05 B10 — poll a redemption
        redemptions.MapGet("/{redemptionId:guid}", async (Guid redemptionId, RedemptionReadsHandler handler, ICurrentUser user, CancellationToken ct) =>
                Results.Ok((await handler.PollAsync(RequireCustomer(user), redemptionId, ct)).OrThrow()))
            .RequireAuthorization().Produces<RedemptionDto>().WithSummary("Poll a redemption (doc 05 B10)");
    }

    private static void MapPosEndpoints(IEndpointRouteBuilder endpoints)
    {
        var pos = endpoints.MapGroup("/v1/pos").WithTags("POS — redemptions");

        // doc 05 D9 [IDEM] — confirm by code
        pos.MapPost("/redemptions/confirm", async (ConfirmRequest request, ConfirmRedemptionHandler handler, ICurrentUser user, CancellationToken ct) =>
                (await handler.HandleAsync(RequireMerchant(user), RequireStaff(user), request.RedemptionCode, ct)).OrThrow())
            .RequirePermission("redemptions.confirm").AddEndpointFilter<Transactions.PublicApi.IdempotencyEndpointFilter>()
            .Produces<ConfirmResponse>().WithSummary("Confirm a redemption (doc 05 D9)");

        // doc 05 D10 [IDEM] — cashier one-step
        pos.MapPost("/redemptions", async (OneStepRequest request, OneStepRedemptionHandler handler, ICurrentUser user, CancellationToken ct) =>
                (await handler.HandleAsync(RequireMerchant(user), RequireStaff(user), request.CustomerId, request.RewardId, ct)).OrThrow())
            .RequirePermission("redemptions.confirm").AddEndpointFilter<Transactions.PublicApi.IdempotencyEndpointFilter>()
            .Produces<ConfirmResponse>().WithSummary("Cashier one-step redemption (doc 05 D10)");

        // doc 05 D11 — cancel (release hold)
        pos.MapPost("/redemptions/{redemptionId:guid}/cancel", async (Guid redemptionId, CancelRedemptionHandler handler, ICurrentUser user, CancellationToken ct) =>
            {
                (await handler.HandleAsync(RequireMerchant(user), redemptionId, ct)).OrThrow();
                return Results.NoContent();
            })
            .RequirePermission("redemptions.confirm").WithSummary("Cancel a redemption (doc 05 D11)");

        // doc 05 F3 — rewards a customer can redeem now (cashier)
        pos.MapGet("/customers/{customerId:guid}/eligibility", async (Guid customerId, EligibilityHandler handler, ICurrentUser user, CancellationToken ct) =>
                Results.Ok(await handler.ForCustomerAsync(RequireMerchant(user), customerId, ct)))
            .RequirePermission("customers.identify").Produces<EligibilityResponse>().WithSummary("Customer reward eligibility (doc 05 F3)");
    }

    private static Guid RequireMerchant(ICurrentUser user) =>
        user.MerchantId ?? throw new ApiException(403, ErrorCodes.Forbidden, "No merchant scope on this token.");

    private static Guid RequireStaff(ICurrentUser user) =>
        user.StaffId ?? throw new ApiException(403, ErrorCodes.Forbidden, "No staff identity on this token.");

    private static Guid RequireCustomer(ICurrentUser user) =>
        user.Id ?? throw new ApiException(401, ErrorCodes.Unauthorized, "Authentication required.");
}
