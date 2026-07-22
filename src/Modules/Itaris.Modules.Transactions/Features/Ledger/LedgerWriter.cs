using System.Data.Common;
using Itaris.Modules.Transactions.Domain;
using Itaris.Modules.Transactions.Persistence;
using Itaris.Modules.Transactions.PublicApi;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Transactions.Features.Ledger;

/// <summary>Implements <see cref="ILedgerWriter"/> by appending to points_ledger_entries on the caller's transaction.</summary>
public sealed class LedgerWriter : ILedgerWriter
{
    public async Task WriteAsync(
        LedgerEntryData data, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken)
    {
        var options = new DbContextOptionsBuilder<TransactionsDbContext>()
            .UseNpgsql(connection)
            .Options;
        await using var db = new TransactionsDbContext(options);
        await db.Database.UseTransactionAsync(transaction, cancellationToken);

        db.Ledger.Add(new PointsLedgerEntry
        {
            MembershipId = data.MembershipId,
            EntryType = data.EntryType,
            PointsDelta = data.PointsDelta,
            StampsDelta = data.StampsDelta,
            BalanceAfter = data.BalanceAfter,
            SourceType = data.SourceType,
            SourceId = data.SourceId,
            Reason = data.Reason,
            CreatedBy = data.CreatedBy,
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
