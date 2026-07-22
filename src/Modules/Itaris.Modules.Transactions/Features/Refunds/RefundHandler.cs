using Itaris.Modules.Loyalty.PublicApi;
using Itaris.Modules.Merchants.PublicApi;
using Itaris.Modules.Transactions.Domain;
using Itaris.Modules.Transactions.Persistence;
using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Itaris.Modules.Transactions.Features.Refunds;

/// <summary>
/// doc 05 D7 — full/partial refund with proportional points clawback via a compensating ledger
/// entry, atomic with the balance update and the transaction-status change. The transaction row is
/// locked FOR UPDATE inside the transaction so concurrent refunds can't both pass the remaining
/// check. Balance may go negative (doc 06). Errors: NOT_FOUND, ALREADY_FULLY_REFUNDED,
/// REFUND_EXCEEDS_REMAINING, APPROVAL_REQUIRED, VALIDATION_ERROR.
/// </summary>
public sealed class RefundHandler(
    TransactionsDbContext db,
    IMerchantGateway merchants,
    ILoyaltyTransactionParticipant loyalty)
{
    public async Task<Result<RefundResponse>> HandleAsync(
        Guid merchantId, Guid staffMemberId, bool canApprove,
        Guid transactionId, RefundRequest request, CancellationToken cancellationToken)
    {
        var isPartial = request.Type == RefundTypes.Partial;
        if (request.Type is not (RefundTypes.Full or RefundTypes.Partial))
        {
            return Fail(ErrorCodes.ValidationError, "Refund type must be 'full' or 'partial'.");
        }

        var tx = await db.Transactions.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == transactionId && t.MerchantId == merchantId, cancellationToken);
        if (tx is null)
        {
            return Fail(ErrorCodes.NotFound, "Transaction not found.");
        }

        if (tx.Status == TransactionStatuses.Refunded)
        {
            return Fail(ErrorCodes.AlreadyFullyRefunded, "This transaction is already fully refunded.");
        }

        var remaining = tx.AmountMinor - tx.RefundedAmountMinor;
        var amount = isPartial ? request.AmountMinor ?? 0 : remaining;
        if (amount <= 0)
        {
            return Fail(ErrorCodes.ValidationError, "Refund amount must be positive.");
        }

        if (amount > remaining)
        {
            return Result<RefundResponse>.Failure(
                ErrorCodes.RefundExceedsRemaining, "Refund exceeds the remaining refundable amount.",
                new Dictionary<string, object?> { ["remainingMinor"] = remaining });
        }

        // Approval gate (doc 01 refund-requires-manager). Over the limit without approve rights → block.
        var limit = await merchants.GetStaffRefundLimitAsync(staffMemberId, cancellationToken);
        if (limit is { } ceiling && amount > ceiling && !canApprove)
        {
            return Fail(ErrorCodes.ApprovalRequired, "This refund exceeds your limit and needs a manager.");
        }

        return await RefundAtomicallyAsync(
            merchantId, staffMemberId, canApprove, transactionId, isPartial, amount, request.Reason, cancellationToken);
    }

    private async Task<Result<RefundResponse>> RefundAtomicallyAsync(
        Guid merchantId, Guid staffMemberId, bool canApprove, Guid transactionId,
        bool isPartial, long amount, string? reason, CancellationToken cancellationToken)
    {
        await using var dbTx = await db.Database.BeginTransactionAsync(cancellationToken);
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var rawTx = dbTx.GetDbTransaction();

        // Lock the transaction row and re-check remaining under the lock.
        var tx = await db.Transactions
            .FromSqlInterpolated($"SELECT *, xmin FROM transactions.transactions WHERE id = {transactionId} FOR UPDATE")
            .FirstAsync(cancellationToken);

        var remaining = tx.AmountMinor - tx.RefundedAmountMinor;
        if (amount > remaining)
        {
            return Result<RefundResponse>.Failure(
                ErrorCodes.RefundExceedsRemaining, "Refund exceeds the remaining refundable amount.",
                new Dictionary<string, object?> { ["remainingMinor"] = remaining });
        }

        // Proportional clawback from the ORIGINAL earn for this transaction.
        var earn = await db.Ledger.AsNoTracking().FirstOrDefaultAsync(
            e => e.SourceType == LedgerSourceTypes.Transaction && e.SourceId == transactionId
                && e.EntryType == LedgerEntryTypes.Earn, cancellationToken);
        var pointsClawback = earn is null ? 0 : (long)Math.Floor((decimal)earn.PointsDelta * amount / tx.AmountMinor);
        var stampsClawback = earn is null ? 0 : (int)Math.Floor((decimal)earn.StampsDelta * amount / tx.AmountMinor);

        var reversal = await loyalty.ApplyReversalAsync(
            tx.MembershipId, -pointsClawback, -stampsClawback, connection, rawTx, cancellationToken);

        var refund = new Refund
        {
            TransactionId = transactionId,
            Type = isPartial ? RefundTypes.Partial : RefundTypes.Full,
            AmountMinor = amount,
            PointsClawback = pointsClawback,
            StampsClawback = stampsClawback,
            Reason = reason,
            RequestedBy = staffMemberId,
            ApprovedBy = canApprove ? staffMemberId : null,
        };
        db.Refunds.Add(refund);

        db.Ledger.Add(new PointsLedgerEntry
        {
            MembershipId = tx.MembershipId,
            EntryType = LedgerEntryTypes.RefundReversal,
            PointsDelta = -pointsClawback,
            StampsDelta = -stampsClawback,
            BalanceAfter = reversal.NewPointsBalance,
            SourceType = LedgerSourceTypes.Refund,
            SourceId = refund.Id,
            Reason = reason,
            CreatedBy = staffMemberId,
        });

        tx.RefundedAmountMinor += amount;
        tx.Status = tx.RefundedAmountMinor >= tx.AmountMinor
            ? TransactionStatuses.Refunded
            : TransactionStatuses.PartiallyRefunded;

        await db.SaveChangesAsync(cancellationToken);
        await dbTx.CommitAsync(cancellationToken);

        return Result<RefundResponse>.Success(new RefundResponse(
            refund.Id, refund.Type, amount,
            new LoyaltyReversalResult(pointsClawback, stampsClawback), tx.Status));
    }

    private static Result<RefundResponse> Fail(string code, string message) =>
        Result<RefundResponse>.Failure(code, message);
}
