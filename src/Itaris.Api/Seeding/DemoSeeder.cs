using Itaris.Infrastructure.Auth;
using Itaris.Modules.Customers.Domain;
using Itaris.Modules.Customers.Persistence;
using Itaris.Modules.Identity.PublicApi;
using Itaris.Modules.Loyalty.Domain;
using Itaris.Modules.Loyalty.Persistence;
using Itaris.Modules.Merchants.Domain;
using Itaris.Modules.Merchants.Persistence;
using Itaris.Modules.Rewards.Domain;
using Itaris.Modules.Rewards.Persistence;
using Itaris.Modules.Transactions.Domain;
using Itaris.Modules.Transactions.Persistence;
using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Api.Seeding;

/// <summary>
/// Development-only demo seed: the doc 05/06 mock world (Washleh Roasters + 3 merchants, staff,
/// customers, ~60 days of transactions, rewards, a redemption) so every flow is demoable in Swagger
/// with realistic, non-zero data. Idempotent — skips if the Washleh merchant already exists.
/// Writes directly through the module DbContexts (the composition root may; modules may not) so it
/// can backdate transactions and set fixed demo codes/credentials.
/// </summary>
public sealed class DemoSeeder(IServiceScopeFactory scopeFactory, ILogger<DemoSeeder> logger)
{
    public const string OwnerPassword = "DemoPass123!";
    public const string CashierPin = "1234";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var merchantsDb = sp.GetRequiredService<MerchantsDbContext>();

        if (await merchantsDb.Merchants.AnyAsync(m => m.Code == "WASHLEH", cancellationToken))
        {
            return; // already seeded
        }

        logger.LogInformation("Seeding demo world…");
        var ctx = new SeedContext(
            sp, merchantsDb, sp.GetRequiredService<IUserDirectory>(), sp.GetRequiredService<ISecretHasher>());

        // The flagship merchant: a café with a stamp card and a free-drink reward.
        var washleh = await ctx.CreateMerchantAsync(
            "washleh roasters", "WASHLEH", "cafe", "واشلة روسترز",
            ProgramTypes.Stamps, cardSize: 9, pointsPerJod: 0,
            "rana@washleh.itaris.local", "omar@washleh.itaris.local",
            rewardEn: "Free drink", rewardAr: "مشروب مجاني", rewardCostType: RewardCostTypes.StampCompletion, pointsCost: null,
            cancellationToken);

        var reef = await ctx.CreateMerchantAsync(
            "reef bakery", "REEF", "bakery", "مخبز ريف",
            ProgramTypes.Points, cardSize: 0, pointsPerJod: 1,
            "reef@itaris.local", "reef-cashier@itaris.local",
            rewardEn: "Free pastry", rewardAr: "معجنات مجانية", rewardCostType: RewardCostTypes.Points, pointsCost: 50,
            cancellationToken);

        await ctx.CreateMerchantAsync(
            "weibdeh cafe", "WEIBDEH", "cafe", "مقهى الويبدة",
            ProgramTypes.Stamps, cardSize: 6, pointsPerJod: 0,
            "weibdeh@itaris.local", "weibdeh-cashier@itaris.local",
            rewardEn: "Free coffee", rewardAr: "قهوة مجانية", rewardCostType: RewardCostTypes.StampCompletion, pointsCost: null,
            cancellationToken);

        await ctx.CreateMerchantAsync(
            "lamsa salon", "LAMSA", "salon", "صالون لمسة",
            ProgramTypes.Points, cardSize: 0, pointsPerJod: 2,
            "lamsa@itaris.local", "lamsa-cashier@itaris.local",
            rewardEn: "10% off", rewardAr: "خصم ١٠٪", rewardCostType: RewardCostTypes.Points, pointsCost: 100,
            cancellationToken);

        // Customers: three app users + one cashier-enrolled (shadow) regular.
        var layla = await ctx.CreateCustomerAsync("+962790000001", "Layla", isShadow: false, cancellationToken);
        var sara = await ctx.CreateCustomerAsync("+962790000003", "Sara", isShadow: false, cancellationToken);
        var abu = await ctx.CreateCustomerAsync("+962790000004", "Abu Mohammad", isShadow: true, cancellationToken);

        // ~60 days of visits at Washleh (stamps). Layla is a regular; Abu Mohammad lapsed (at-risk).
        await ctx.SeedVisitsAsync(washleh, layla, visits: 18, spreadDays: 55, gapDays: 3, cancellationToken);
        await ctx.SeedVisitsAsync(washleh, sara, visits: 8, spreadDays: 50, gapDays: 6, cancellationToken);
        await ctx.SeedVisitsAsync(washleh, abu, visits: 3, spreadDays: 55, gapDays: 3, ct: cancellationToken, oldestFirstOnly: true);

        // Points at Reef for Layla (enough to earn the 50-pt pastry), then a completed redemption.
        await ctx.SeedVisitsAsync(reef, layla, visits: 20, spreadDays: 50, gapDays: 2, cancellationToken);
        await ctx.SeedPointsRedemptionAsync(reef, layla, "Free pastry", cancellationToken);

        logger.LogInformation("Demo world seeded: 4 merchants, 3 customers, transactions + a redemption.");
    }
}
