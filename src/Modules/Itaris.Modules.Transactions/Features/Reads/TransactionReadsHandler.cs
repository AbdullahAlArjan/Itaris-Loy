using Itaris.Modules.Loyalty.PublicApi;
using Itaris.Modules.Transactions.Persistence;
using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Transactions.Features.Reads;

public sealed record RefundLine(Guid RefundId, string Type, long AmountMinor, DateTimeOffset At);

public sealed record TransactionDto(
    Guid Id, long AmountMinor, string Currency, string Status,
    long RefundedAmountMinor, DateTimeOffset RecordedAt, IReadOnlyList<RefundLine> Refunds);

public sealed record TransactionListItem(
    Guid Id, long AmountMinor, string Status, DateTimeOffset RecordedAt);

public sealed record TransactionListResponse(IReadOnlyList<TransactionListItem> Items);

public sealed record LedgerEntryDto(
    Guid Id, string Type, long PointsDelta, int StampsDelta, long BalanceAfter,
    string SourceType, string? Reason, DateTimeOffset CreatedAt);

public sealed record LedgerResponse(IReadOnlyList<LedgerEntryDto> Items);

/// <summary>doc 05 D5/D6 (POS transaction reads) and B6 (customer ledger).</summary>
public sealed class TransactionReadsHandler(TransactionsDbContext db, ILoyaltyTransactionParticipant loyalty)
{
    public async Task<Result<TransactionDto>> GetDetailAsync(
        Guid merchantId, Guid transactionId, CancellationToken cancellationToken)
    {
        var tx = await db.Transactions.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == transactionId && t.MerchantId == merchantId, cancellationToken);
        if (tx is null)
        {
            return Result<TransactionDto>.Failure(ErrorCodes.NotFound, "Transaction not found.");
        }

        var refunds = await db.Refunds.AsNoTracking()
            .Where(r => r.TransactionId == transactionId)
            .OrderBy(r => r.Id)
            .Select(r => new RefundLine(r.Id, r.Type, r.AmountMinor, r.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<TransactionDto>.Success(new TransactionDto(
            tx.Id, tx.AmountMinor, tx.Currency, tx.Status, tx.RefundedAmountMinor, tx.RecordedAt, refunds));
    }

    public async Task<TransactionListResponse> ListAsync(
        Guid merchantId, Guid? branchId, int limit, CancellationToken cancellationToken)
    {
        var query = db.Transactions.AsNoTracking().Where(t => t.MerchantId == merchantId);
        if (branchId is { } branch)
        {
            query = query.Where(t => t.BranchId == branch);
        }

        var items = await query
            .OrderByDescending(t => t.RecordedAt)
            .Take(Math.Clamp(limit, 1, 100))
            .Select(t => new TransactionListItem(t.Id, t.AmountMinor, t.Status, t.RecordedAt))
            .ToListAsync(cancellationToken);

        return new TransactionListResponse(items);
    }

    public async Task<Result<LedgerResponse>> GetCustomerLedgerAsync(
        Guid customerId, Guid membershipId, CancellationToken cancellationToken)
    {
        var owner = await loyalty.GetMembershipOwnerAsync(membershipId, cancellationToken);
        if (owner != customerId)
        {
            return Result<LedgerResponse>.Failure(ErrorCodes.NotFound, "Membership not found.");
        }

        var items = await db.Ledger.AsNoTracking()
            .Where(e => e.MembershipId == membershipId)
            .OrderByDescending(e => e.Id) // v7 id = chronological
            .Take(100)
            .Select(e => new LedgerEntryDto(
                e.Id, e.EntryType, e.PointsDelta, e.StampsDelta, e.BalanceAfter,
                e.SourceType, e.Reason, e.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<LedgerResponse>.Success(new LedgerResponse(items));
    }
}
