using Itaris.Modules.Loyalty.PublicApi;
using Itaris.Modules.Rewards.Domain;
using Itaris.Modules.Rewards.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Rewards.Features.ManageRewards;

/// <summary>
/// doc 05 B7 (customer) / F3 (POS) — the merchant's active rewards annotated with whether THIS
/// customer can redeem each now: enough points (points reward) or a completed card (stamp reward),
/// and in stock. Read-only over the membership snapshot from Loyalty.
/// </summary>
public sealed class EligibilityHandler(RewardsDbContext db, ILoyaltyTransactionParticipant loyalty)
{
    public async Task<EligibilityResponse> ForCustomerAsync(
        Guid merchantId, Guid customerId, CancellationToken cancellationToken)
    {
        var rewards = await db.Rewards.AsNoTracking()
            .Where(r => r.MerchantId == merchantId && r.Status == RewardStatuses.Active)
            .OrderByDescending(r => r.Id)
            .ToListAsync(cancellationToken);

        var snapshot = await loyalty.GetMembershipSnapshotAsync(merchantId, customerId, cancellationToken);
        var points = snapshot?.NewPointsBalance ?? 0;
        var completedCards = snapshot?.CardCycle ?? 0;

        var items = rewards.Select(r =>
        {
            var inStock = r.StockRemaining is null or > 0;
            bool eligible;
            long? missing = null;

            if (r.CostType == RewardCostTypes.StampCompletion)
            {
                eligible = inStock && completedCards >= 1;
            }
            else
            {
                var cost = r.PointsCost ?? 0;
                eligible = inStock && points >= cost;
                missing = Math.Max(0, cost - points);
            }

            return new EligibleRewardDto(r.Id, r.NameAr, r.NameEn, r.CostType, r.PointsCost, eligible, missing, inStock);
        }).ToList();

        return new EligibilityResponse(items);
    }
}
