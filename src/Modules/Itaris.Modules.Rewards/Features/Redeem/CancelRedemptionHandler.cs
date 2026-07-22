using Itaris.Modules.Rewards.Domain;
using Itaris.Modules.Rewards.Persistence;
using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Itaris.Modules.Rewards.Features.Redeem;

/// <summary>doc 05 D11 — cancels a pending redemption, releasing its hold. Errors: REDEMPTION_NOT_FOUND.</summary>
public sealed class CancelRedemptionHandler(RewardsDbContext db, RedemptionReleaser releaser)
{
    public async Task<Result<bool>> HandleAsync(
        Guid merchantId, Guid redemptionId, CancellationToken cancellationToken)
    {
        await using var dbTx = await db.Database.BeginTransactionAsync(cancellationToken);
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var rawTx = dbTx.GetDbTransaction();

        var redemption = await db.Redemptions
            .FromSqlInterpolated($"SELECT *, xmin FROM rewards.redemptions WHERE id = {redemptionId} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);

        if (redemption is null || redemption.MerchantId != merchantId)
        {
            return Result<bool>.Failure(ErrorCodes.RedemptionNotFound, "Redemption not found.");
        }

        if (redemption.Status != RedemptionStatuses.Pending)
        {
            return Result<bool>.Failure(ErrorCodes.RedemptionNotFound, "Only a pending redemption can be cancelled.");
        }

        await releaser.ReleaseAsync(db, redemption, RedemptionStatuses.Cancelled, connection, rawTx, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await dbTx.CommitAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
