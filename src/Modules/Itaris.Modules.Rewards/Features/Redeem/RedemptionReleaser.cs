using Itaris.Modules.Loyalty.PublicApi;
using Itaris.Modules.Rewards.Domain;
using Itaris.Modules.Rewards.Persistence;
using Itaris.Modules.Transactions.PublicApi;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Itaris.Modules.Rewards.Features.Redeem;

/// <summary>
/// Releases a pending redemption's hold (cancel or TTL-expiry): restores the reward stock, restores
/// the held points / completed card via Loyalty, writes a compensating ledger entry, and sets the
/// redemption's terminal status. Must run inside a caller-owned transaction on <paramref name="db"/>.
/// </summary>
public sealed class RedemptionReleaser(ILoyaltyTransactionParticipant loyalty, ILedgerWriter ledger)
{
    public async Task ReleaseAsync(
        RewardsDbContext db, Redemption redemption, string terminalStatus,
        NpgsqlConnection connection, System.Data.Common.DbTransaction transaction, CancellationToken cancellationToken)
    {
        // Restore stock.
        var reward = await db.Rewards
            .FromSqlInterpolated($"SELECT *, xmin FROM rewards.rewards WHERE id = {redemption.RewardId} FOR UPDATE")
            .FirstAsync(cancellationToken);
        if (reward.StockRemaining is not null)
        {
            reward.StockRemaining++;
        }

        // Restore the held points / completed card.
        await loyalty.ReleaseRedemptionHoldAsync(
            redemption.MembershipId, redemption.PointsHeld, redemption.StampCardConsumed,
            connection, transaction, cancellationToken);

        // Compensating ledger entry (does not need balance_after precision here; record the restore).
        await ledger.WriteAsync(
            new LedgerEntryData(
                redemption.MembershipId, LedgerEntryTypesRewards.Redeem, redemption.PointsHeld, 0,
                0, LedgerSourceTypesRewards.Redemption, redemption.Id,
                $"redemption_release:{terminalStatus}", null),
            connection, transaction, cancellationToken);

        redemption.Status = terminalStatus;
    }
}
