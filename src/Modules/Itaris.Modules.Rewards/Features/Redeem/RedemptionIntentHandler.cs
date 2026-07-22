using Itaris.Modules.Loyalty.PublicApi;
using Itaris.Modules.Rewards.Domain;
using Itaris.Modules.Rewards.Persistence;
using Itaris.Modules.Transactions.PublicApi;
using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Itaris.Modules.Rewards.Features.Redeem;

/// <summary>
/// doc 05 B9 — creates a redemption intent (the hold). Atomically: locks the reward and decrements
/// stock, holds the cost via Loyalty (points deducted / completed card consumed) under a membership
/// row lock, writes the deduction ledger entry through the Transactions single-writer, and creates a
/// pending redemption with a 5-minute TTL (doc 06 freeze). Only one pending redemption per
/// customer+merchant (partial unique index → PENDING_REDEMPTION_EXISTS).
/// Errors: REWARD_INACTIVE, REWARD_OUT_OF_STOCK, INSUFFICIENT_POINTS, PENDING_REDEMPTION_EXISTS, NOT_FOUND.
/// </summary>
public sealed class RedemptionIntentHandler(
    RewardsDbContext db,
    ILoyaltyTransactionParticipant loyalty,
    ILedgerWriter ledger,
    IClock clock)
{
    public const int TtlMinutes = 5;

    public async Task<Result<IntentResponse>> HandleAsync(
        Guid merchantId, Guid customerId, Guid rewardId, Guid? confirmedByStaff, CancellationToken cancellationToken)
    {
        var reward = await db.Rewards.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == rewardId && r.MerchantId == merchantId, cancellationToken);
        if (reward is null)
        {
            return Fail(ErrorCodes.NotFound, "Reward not found.");
        }

        if (reward.Status != RewardStatuses.Active)
        {
            return Fail(ErrorCodes.RewardInactive, "This reward is not active.");
        }

        var snapshot = await loyalty.GetMembershipSnapshotAsync(merchantId, customerId, cancellationToken);
        if (snapshot is null)
        {
            return Fail(ErrorCodes.InsufficientPoints, "You are not a member of this program yet.");
        }

        var consumeCard = reward.CostType == RewardCostTypes.StampCompletion;
        var pointsCost = consumeCard ? 0 : reward.PointsCost ?? 0;

        await using var dbTx = await db.Database.BeginTransactionAsync(cancellationToken);
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var rawTx = dbTx.GetDbTransaction();

        // Lock the reward row and re-check stock under the lock (out-of-stock race, doc 06).
        var lockedReward = await db.Rewards
            .FromSqlInterpolated($"SELECT *, xmin FROM rewards.rewards WHERE id = {rewardId} FOR UPDATE")
            .FirstAsync(cancellationToken);
        if (lockedReward.StockRemaining is <= 0)
        {
            return Fail(ErrorCodes.RewardOutOfStock, "This reward is out of stock.");
        }

        if (lockedReward.StockRemaining is not null)
        {
            lockedReward.StockRemaining--;
        }

        var hold = await loyalty.ApplyRedemptionHoldAsync(
            snapshot.MembershipId, pointsCost, consumeCard, connection, rawTx, cancellationToken);
        if (hold.Failure != HoldFailure.None)
        {
            return Fail(ErrorCodes.InsufficientPoints,
                consumeCard ? "No completed stamp card to redeem." : "Not enough points for this reward.");
        }

        var redemption = new Redemption
        {
            MembershipId = snapshot.MembershipId,
            CustomerId = customerId,
            MerchantId = merchantId,
            RewardId = rewardId,
            Status = RedemptionStatuses.Pending,
            Code = RedemptionCode.Generate(),
            PointsHeld = pointsCost,
            StampCardConsumed = consumeCard,
            ExpiresAt = clock.UtcNow.AddMinutes(TtlMinutes),
            ConfirmedByStaffId = confirmedByStaff,
        };
        db.Redemptions.Add(redemption);

        await ledger.WriteAsync(
            new LedgerEntryData(
                snapshot.MembershipId, LedgerEntryTypesRewards.Redeem, -pointsCost, 0,
                hold.NewPointsBalance, LedgerSourceTypesRewards.Redemption, redemption.Id,
                $"redemption:{rewardId}", confirmedByStaff),
            connection, rawTx, cancellationToken);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Lost the one-pending-per-customer race (partial unique index).
            return Fail(ErrorCodes.PendingRedemptionExists, "You already have a pending redemption here.");
        }

        await dbTx.CommitAsync(cancellationToken);

        return Result<IntentResponse>.Success(new IntentResponse(
            redemption.Id, redemption.Code, redemption.Status, redemption.ExpiresAt,
            redemption.PointsHeld, redemption.StampCardConsumed));
    }

    private static Result<IntentResponse> Fail(string code, string message) =>
        Result<IntentResponse>.Failure(code, message);
}

/// <summary>Mirror of the Transactions ledger vocabulary used from the Rewards side.</summary>
internal static class LedgerEntryTypesRewards
{
    public const string Redeem = "redeem";
}

internal static class LedgerSourceTypesRewards
{
    public const string Redemption = "redemption";
}
