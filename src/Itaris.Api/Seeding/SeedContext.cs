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
using Microsoft.EntityFrameworkCore;

namespace Itaris.Api.Seeding;

internal sealed record MerchantHandle(
    Guid MerchantId, Guid BranchId, Guid CashierStaffId, Guid ProgramId, Guid RuleId,
    string ProgramType, int CardSize, decimal PointsPerJod, Guid RewardId);

/// <summary>Builder helpers for <see cref="DemoSeeder"/>, sharing one DI scope across the module DbContexts.</summary>
internal sealed class SeedContext(
    IServiceProvider sp, MerchantsDbContext merchants, IUserDirectory users, ISecretHasher hasher)
{
    private readonly LoyaltyDbContext _loyalty = sp.GetRequiredService<LoyaltyDbContext>();
    private readonly CustomersDbContext _customers = sp.GetRequiredService<CustomersDbContext>();
    private readonly TransactionsDbContext _tx = sp.GetRequiredService<TransactionsDbContext>();
    private readonly RewardsDbContext _rewards = sp.GetRequiredService<RewardsDbContext>();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    public async Task<MerchantHandle> CreateMerchantAsync(
        string nameEn, string code, string category, string nameAr,
        string programType, int cardSize, decimal pointsPerJod,
        string ownerEmail, string cashierEmail,
        string rewardEn, string rewardAr, string rewardCostType, long? pointsCost,
        CancellationToken ct)
    {
        var ownerUserId = await users.CreateOwnerAsync(ownerEmail, DemoSeeder.OwnerPassword, ct);
        var cashierUserId = await users.CreateStaffUserAsync(cashierEmail, ct);

        var merchant = new Merchant
        {
            Code = code, NameAr = nameAr, NameEn = Title(nameEn), Category = category, Status = MerchantStatuses.Active,
        };
        merchants.Merchants.Add(merchant);

        var branch = new Branch { MerchantId = merchant.Id, NameAr = nameAr, NameEn = Title(nameEn), IsActive = true };
        merchants.Branches.Add(branch);

        var owner = new StaffMember { MerchantId = merchant.Id, UserId = ownerUserId, DisplayName = Title(nameEn), PhoneOrEmail = ownerEmail, Status = StaffStatuses.Active };
        var cashier = new StaffMember { MerchantId = merchant.Id, UserId = cashierUserId, DisplayName = "Cashier", PhoneOrEmail = cashierEmail, Status = StaffStatuses.Active, PinHash = hasher.Hash(DemoSeeder.CashierPin) };
        merchants.StaffMembers.AddRange(owner, cashier);
        merchants.StaffRoles.Add(new StaffRole { StaffMemberId = owner.Id, RoleId = DeterministicGuid.Create($"role:{SystemRoles.Owner}") });
        merchants.StaffRoles.Add(new StaffRole { StaffMemberId = cashier.Id, RoleId = DeterministicGuid.Create($"role:{SystemRoles.Cashier}") });
        await merchants.SaveChangesAsync(ct);

        var program = new LoyaltyProgram { MerchantId = merchant.Id, Type = programType, NameAr = "برنامج الولاء", NameEn = "Loyalty Program", Status = ProgramStatuses.Active };
        _loyalty.Programs.Add(program);
        var rule = new LoyaltyRule
        {
            ProgramId = program.Id,
            Version = 1,
            EffectiveFrom = Now.AddDays(-60),
            ConfigJson = LoyaltyJson.Serialize(new RuleConfig
            {
                PointsPerJod = pointsPerJod, Rounding = RoundingMode.Floor, CardSize = cardSize, StampsPerVisit = 1, MaxStampsPerVisit = 1,
            }),
        };
        _loyalty.Rules.Add(rule);
        program.CurrentRuleId = rule.Id;
        await _loyalty.SaveChangesAsync(ct);

        var reward = new Reward
        {
            MerchantId = merchant.Id, NameAr = rewardAr, NameEn = rewardEn, CostType = rewardCostType,
            PointsCost = pointsCost, Status = RewardStatuses.Active,
        };
        _rewards.Rewards.Add(reward);
        await _rewards.SaveChangesAsync(ct);

        return new MerchantHandle(merchant.Id, branch.Id, cashier.Id, program.Id, rule.Id, programType, cardSize, pointsPerJod, reward.Id);
    }

    public async Task<Guid> CreateCustomerAsync(string phone, string firstName, bool isShadow, CancellationToken ct)
    {
        var userId = await users.EnsureCustomerByPhoneAsync(phone, ct);
        _customers.Profiles.Add(new CustomerProfile
        {
            UserId = userId, PhoneNumber = phone, FirstName = firstName, PreferredLanguage = "ar",
            IsShadow = isShadow, ClaimedAt = isShadow ? null : Now,
        });
        await _customers.SaveChangesAsync(ct);
        return userId;
    }

    public async Task SeedVisitsAsync(
        MerchantHandle m, Guid customerId, int visits, int spreadDays, int gapDays,
        CancellationToken ct, bool oldestFirstOnly = false)
    {
        var membership = new CustomerMembership
        {
            CustomerId = customerId, MerchantId = m.MerchantId, ProgramId = m.ProgramId,
            JoinedAt = Now.AddDays(-spreadDays), JoinSource = MembershipJoinSources.Counter,
        };
        _loyalty.Memberships.Add(membership);
        await _loyalty.SaveChangesAsync(ct);

        long points = 0;
        var stamps = 0;
        var cycle = 0;

        for (var i = 0; i < visits; i++)
        {
            var when = oldestFirstOnly
                ? Now.AddDays(-spreadDays + i * gapDays)                       // clustered in the past → at-risk
                : Now.AddDays(-spreadDays + i * ((double)spreadDays / visits)); // spread up to now
            var amountMinor = 3000 + (i % 4) * 500; // 3.000–4.500 JOD

            int stampsDelta = 0;
            long pointsDelta = 0;
            if (m.ProgramType == ProgramTypes.Stamps)
            {
                stampsDelta = 1;
                stamps += 1;
                if (m.CardSize > 0 && stamps >= m.CardSize)
                {
                    stamps -= m.CardSize;
                    cycle += 1;
                }
            }
            else
            {
                pointsDelta = (long)Math.Floor(amountMinor * m.PointsPerJod / 1000m);
                points += pointsDelta;
            }

            var transaction = new Transaction
            {
                MerchantId = m.MerchantId, BranchId = m.BranchId, MembershipId = membership.Id, StaffMemberId = m.CashierStaffId,
                AmountMinor = amountMinor, Status = TransactionStatuses.Completed,
                OccurredAt = when, RecordedAt = when, Source = TransactionSources.Cashier, RuleId = m.RuleId,
            };
            _tx.Transactions.Add(transaction);
            _tx.Ledger.Add(new PointsLedgerEntry
            {
                MembershipId = membership.Id, EntryType = LedgerEntryTypes.Earn,
                PointsDelta = pointsDelta, StampsDelta = stampsDelta, BalanceAfter = points,
                SourceType = LedgerSourceTypes.Transaction, SourceId = transaction.Id, CreatedBy = m.CashierStaffId,
            });
        }

        membership.PointsBalance = points;
        membership.StampsFilled = stamps;
        membership.StampCardCycle = cycle;
        await _tx.SaveChangesAsync(ct);
        await _loyalty.SaveChangesAsync(ct);
    }

    public async Task SeedPointsRedemptionAsync(MerchantHandle m, Guid customerId, string rewardName, CancellationToken ct)
    {
        var membership = await _loyalty.Memberships.FirstOrDefaultAsync(
            x => x.CustomerId == customerId && x.MerchantId == m.MerchantId, ct);
        var reward = await _rewards.Rewards.FirstOrDefaultAsync(x => x.Id == m.RewardId, ct);
        if (membership is null || reward?.PointsCost is not { } cost || membership.PointsBalance < cost)
        {
            return;
        }

        membership.PointsBalance -= cost;
        await _loyalty.SaveChangesAsync(ct);

        _rewards.Redemptions.Add(new Redemption
        {
            MembershipId = membership.Id, CustomerId = customerId, MerchantId = m.MerchantId, RewardId = m.RewardId,
            Status = RedemptionStatuses.Completed, Code = RedemptionCode.Generate(), PointsHeld = cost,
            ExpiresAt = Now.AddDays(-10), ConfirmedAt = Now.AddDays(-10), ConfirmedByStaffId = m.CashierStaffId,
        });
        _tx.Ledger.Add(new PointsLedgerEntry
        {
            MembershipId = membership.Id, EntryType = LedgerEntryTypes.Redeem, PointsDelta = -cost, StampsDelta = 0,
            BalanceAfter = membership.PointsBalance, SourceType = LedgerSourceTypes.Redemption, SourceId = Guid.NewGuid(),
            Reason = $"redemption:{rewardName}", CreatedBy = m.CashierStaffId,
        });
        await _rewards.SaveChangesAsync(ct);
        await _tx.SaveChangesAsync(ct);
    }

    private static string Title(string s) => string.Join(' ', s.Split(' ').Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
}
