using Itaris.Modules.Rewards.Domain;
using Itaris.Modules.Rewards.Persistence;
using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Itaris.Modules.Rewards.Features.Redeem;

/// <summary>
/// doc 05 D9 — cashier confirms a redemption by code. THE double-redemption defense: the redemption
/// row is locked FOR UPDATE, so of N parallel confirms of one code exactly one sees it 'pending' and
/// completes it; the rest see 'completed' → REDEMPTION_ALREADY_USED. Points were already deducted at
/// intent, so confirm only finalizes status. An expired pending intent is released here instead.
/// Errors: REDEMPTION_NOT_FOUND, REDEMPTION_EXPIRED, REDEMPTION_ALREADY_USED.
/// </summary>
public sealed class ConfirmRedemptionHandler(RewardsDbContext db, RedemptionReleaser releaser, IClock clock)
{
    public async Task<Result<ConfirmResponse>> HandleAsync(
        Guid merchantId, Guid staffId, string redemptionCode, CancellationToken cancellationToken)
    {
        var code = redemptionCode.Trim().ToUpperInvariant();

        await using var dbTx = await db.Database.BeginTransactionAsync(cancellationToken);
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var rawTx = dbTx.GetDbTransaction();

        var redemption = await db.Redemptions
            .FromSqlInterpolated($"SELECT *, xmin FROM rewards.redemptions WHERE code = {code} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);

        if (redemption is null || redemption.MerchantId != merchantId)
        {
            return Fail(ErrorCodes.RedemptionNotFound, "No redemption found for that code.");
        }

        if (redemption.Status == RedemptionStatuses.Completed)
        {
            return Fail(ErrorCodes.RedemptionAlreadyUsed, "This redemption was already used.");
        }

        if (redemption.Status is RedemptionStatuses.Cancelled or RedemptionStatuses.Expired)
        {
            return Fail(ErrorCodes.RedemptionNotFound, "This redemption is no longer valid.");
        }

        if (clock.UtcNow >= redemption.ExpiresAt)
        {
            await releaser.ReleaseAsync(db, redemption, RedemptionStatuses.Expired, connection, rawTx, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await dbTx.CommitAsync(cancellationToken);
            return Fail(ErrorCodes.RedemptionExpired, "This redemption has expired.");
        }

        redemption.Status = RedemptionStatuses.Completed;
        redemption.ConfirmedAt = clock.UtcNow;
        redemption.ConfirmedByStaffId = staffId;

        await db.SaveChangesAsync(cancellationToken);
        await dbTx.CommitAsync(cancellationToken);

        return Result<ConfirmResponse>.Success(
            new ConfirmResponse(redemption.Id, redemption.Status, redemption.ConfirmedAt.Value));
    }

    private static Result<ConfirmResponse> Fail(string code, string message) =>
        Result<ConfirmResponse>.Failure(code, message);
}
