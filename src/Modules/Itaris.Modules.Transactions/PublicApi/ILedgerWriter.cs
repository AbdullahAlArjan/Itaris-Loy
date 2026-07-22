using System.Data.Common;

namespace Itaris.Modules.Transactions.PublicApi;

public sealed record LedgerEntryData(
    Guid MembershipId,
    string EntryType,
    long PointsDelta,
    int StampsDelta,
    long BalanceAfter,
    string SourceType,
    Guid SourceId,
    string? Reason,
    Guid? CreatedBy);

/// <summary>
/// Single-writer ledger contract (doc 04: the Transactions module is the only writer of
/// points_ledger_entries; other modules — e.g. Rewards redemption deductions — write through this).
/// Enlists on the caller's connection/transaction so the ledger entry is atomic with the caller's work.
/// </summary>
public interface ILedgerWriter
{
    Task WriteAsync(
        LedgerEntryData data, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken);
}
