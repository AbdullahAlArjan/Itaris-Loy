using Itaris.Modules.Transactions.Domain;
using Itaris.Modules.Transactions.Persistence;
using Itaris.Modules.Transactions.PublicApi;
using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Transactions.Features.Analytics;

/// <summary>
/// Computes the merchant overview from transactions + the ledger. "Visits" = recorded sales;
/// "at-risk" = members whose most recent visit is older than the at-risk window (doc 01: at-risk
/// customers surfaced early). All merchant-scoped.
/// </summary>
public sealed class TransactionAnalytics(TransactionsDbContext db, IClock clock) : ITransactionAnalytics
{
    private const int AtRiskDays = 14;

    public async Task<AnalyticsOverview> GetOverviewAsync(
        Guid merchantId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var fromTs = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toTs = new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        var inRange = db.Transactions.AsNoTracking()
            .Where(t => t.MerchantId == merchantId && t.RecordedAt >= fromTs && t.RecordedAt <= toTs);

        // Visits per membership in the window → active members + repeat rate.
        var perMember = await inRange
            .GroupBy(t => t.MembershipId)
            .Select(g => new { Membership = g.Key, Visits = g.Count() })
            .ToListAsync(cancellationToken);
        var activeMembers = perMember.Count;
        var repeatVisitRate = activeMembers == 0
            ? 0
            : Math.Round((double)perMember.Count(m => m.Visits >= 2) / activeMembers, 2);

        var weekAgo = clock.UtcNow.AddDays(-7);
        var visitsThisWeek = await db.Transactions.AsNoTracking()
            .CountAsync(t => t.MerchantId == merchantId && t.RecordedAt >= weekAgo, cancellationToken);

        var series = (await inRange
                .GroupBy(t => t.RecordedAt.Date)
                .Select(g => new { Day = g.Key, Visits = g.Count() })
                .ToListAsync(cancellationToken))
            .Select(x => new VisitsPoint(DateOnly.FromDateTime(x.Day), x.Visits))
            .OrderBy(p => p.Date)
            .ToList();

        var ledgerInRange = db.Ledger.AsNoTracking()
            .Where(e => e.CreatedAt >= fromTs && e.CreatedAt <= toTs
                && inRange.Select(t => t.MembershipId).Contains(e.MembershipId));
        var pointsIssued = await ledgerInRange
            .Where(e => e.EntryType == LedgerEntryTypes.Earn)
            .SumAsync(e => (long?)e.PointsDelta, cancellationToken) ?? 0;
        var pointsRedeemed = await ledgerInRange
            .Where(e => e.EntryType == LedgerEntryTypes.Redeem)
            .SumAsync(e => (long?)e.PointsDelta, cancellationToken) ?? 0;

        // At-risk: members whose last-ever visit is older than the window.
        var atRiskCutoff = clock.UtcNow.AddDays(-AtRiskDays);
        var atRiskCustomers = await db.Transactions.AsNoTracking()
            .Where(t => t.MerchantId == merchantId)
            .GroupBy(t => t.MembershipId)
            .Select(g => g.Max(t => t.RecordedAt))
            .CountAsync(last => last < atRiskCutoff, cancellationToken);

        var topBranchId = await inRange
            .GroupBy(t => t.BranchId)
            .OrderByDescending(g => g.Count())
            .Select(g => (Guid?)g.Key)
            .FirstOrDefaultAsync(cancellationToken);

        return new AnalyticsOverview(
            from, to, visitsThisWeek, repeatVisitRate, activeMembers,
            pointsIssued, Math.Abs(pointsRedeemed), atRiskCustomers, series, topBranchId);
    }
}
