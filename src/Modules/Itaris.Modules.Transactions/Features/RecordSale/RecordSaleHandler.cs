using Itaris.Modules.Customers.PublicApi;
using Itaris.Modules.Loyalty.PublicApi;
using Itaris.Modules.Merchants.Domain;
using Itaris.Modules.Merchants.PublicApi;
using Itaris.Modules.Transactions.Domain;
using Itaris.Modules.Transactions.Persistence;
using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Itaris.Modules.Transactions.Features.RecordSale;

/// <summary>
/// doc 05 D4 / §9.8 — records a sale atomically: the transaction row, the immutable ledger entry,
/// and the membership balance/stamp projection all commit (or roll back) in ONE database transaction.
/// The Loyalty participant enlists in that transaction and row-locks the membership (SELECT … FOR
/// UPDATE), so 20 concurrent sales on one membership yield the correct final balance (doc 06 test).
/// Enforces merchant-paused block, amount limit, and duplicate detection with an override flow.
/// Errors: PROGRAM_INACTIVE, AMOUNT_EXCEEDS_LIMIT, DUPLICATE_SUSPECTED, VALIDATION_ERROR, NOT_FOUND.
/// </summary>
public sealed class RecordSaleHandler(
    TransactionsDbContext db,
    IMerchantGateway merchants,
    ILoyaltyTransactionParticipant loyalty,
    ICustomerDirectory customers,
    IClock clock)
{
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromMinutes(2);

    public async Task<Result<RecordSaleResponse>> HandleAsync(
        Guid merchantId, Guid staffMemberId, RecordSaleRequest request, CancellationToken cancellationToken)
    {
        if (request.AmountMinor <= 0)
        {
            return Fail(ErrorCodes.ValidationError, "Amount must be positive.");
        }

        var context = await merchants.GetPosContextAsync(merchantId, request.BranchId, cancellationToken);
        if (context is null)
        {
            return Fail(ErrorCodes.MerchantNotFound, "Merchant not found.");
        }

        if (context.Status == MerchantStatuses.Paused)
        {
            return Fail(ErrorCodes.ProgramInactive, "This merchant is paused.");
        }

        if (!context.BranchValid)
        {
            return Fail(ErrorCodes.ValidationError, "Unknown or inactive branch for this merchant.");
        }

        if (context.MaxTransactionAmountMinor is { } max && request.AmountMinor > max)
        {
            return Result<RecordSaleResponse>.Failure(
                ErrorCodes.AmountExceedsLimit, "Amount exceeds this merchant's per-transaction limit.",
                new Dictionary<string, object?> { ["limitMinor"] = max });
        }

        // Duplicate detection: same membership + amount within a short window (doc 05 D4).
        if (!request.DuplicateOverride)
        {
            var snapshot = await loyalty.GetMembershipSnapshotAsync(merchantId, request.CustomerId, cancellationToken);
            if (snapshot is not null)
            {
                var since = clock.UtcNow - DuplicateWindow;
                var original = await db.Transactions.AsNoTracking()
                    .Where(t => t.MembershipId == snapshot.MembershipId
                        && t.AmountMinor == request.AmountMinor
                        && t.RecordedAt >= since)
                    .OrderByDescending(t => t.RecordedAt)
                    .FirstOrDefaultAsync(cancellationToken);
                if (original is not null)
                {
                    return Result<RecordSaleResponse>.Failure(
                        ErrorCodes.DuplicateSuspected, "A matching sale was just recorded.",
                        new Dictionary<string, object?> { ["originalTransactionId"] = original.Id });
                }
            }
        }

        return await RecordAtomicallyAsync(merchantId, staffMemberId, context, request, cancellationToken);
    }

    private async Task<Result<RecordSaleResponse>> RecordAtomicallyAsync(
        Guid merchantId, Guid staffMemberId, MerchantPosContext context,
        RecordSaleRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        await using var dbTx = await db.Database.BeginTransactionAsync(cancellationToken);
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var rawTx = dbTx.GetDbTransaction();

        var outcome = await loyalty.ApplyEarnAsync(
            merchantId, request.CustomerId, request.AmountMinor, connection, rawTx, cancellationToken);
        if (outcome.Applied is not { } earn)
        {
            return Fail(ErrorCodes.ProgramInactive, "This merchant has no active loyalty program.");
        }

        var transaction = new Transaction
        {
            MerchantId = merchantId,
            BranchId = request.BranchId,
            MembershipId = earn.MembershipId,
            StaffMemberId = staffMemberId,
            AmountMinor = request.AmountMinor,
            Currency = string.IsNullOrEmpty(request.Currency) ? Money.Jod : request.Currency,
            Note = request.Note,
            Status = TransactionStatuses.Completed,
            OccurredAt = request.OccurredAt ?? now,
            RecordedAt = now,
            Source = TransactionSources.Cashier,
            RuleId = earn.RuleId,
        };
        db.Transactions.Add(transaction);

        db.Ledger.Add(new PointsLedgerEntry
        {
            MembershipId = earn.MembershipId,
            EntryType = LedgerEntryTypes.Earn,
            PointsDelta = earn.PointsEarned,
            StampsDelta = earn.StampsEarned,
            BalanceAfter = earn.NewPointsBalance,
            SourceType = LedgerSourceTypes.Transaction,
            SourceId = transaction.Id,
            CreatedBy = staffMemberId,
        });

        await db.SaveChangesAsync(cancellationToken);
        await dbTx.CommitAsync(cancellationToken);

        var summary = await customers.GetSummaryAsync(request.CustomerId, cancellationToken);
        return Result<RecordSaleResponse>.Success(BuildResponse(transaction, earn, summary, now));
    }

    private static RecordSaleResponse BuildResponse(
        Transaction tx, EarnApplication earn, CustomerSummary? customer, DateTimeOffset now)
    {
        StampCardResult? card = earn.ProgramType == "stamps"
            ? new StampCardResult(earn.StampsFilled, earn.CardSize, earn.CardCompleted, earn.CardCycle)
            : null;

        var loyalty = new LoyaltyResult(
            earn.ProgramType, earn.StampsEarned, card, earn.PointsEarned, earn.NewPointsBalance);

        return new RecordSaleResponse(
            tx.Id, tx.Status, tx.AmountMinor, loyalty,
            new SaleCustomerResult(customer?.FirstName, earn.IsNewMember), now);
    }

    private static Result<RecordSaleResponse> Fail(string code, string message) =>
        Result<RecordSaleResponse>.Failure(code, message);
}
