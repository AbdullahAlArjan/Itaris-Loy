namespace Itaris.Modules.Transactions.PublicApi;

/// <summary>The merchant "5 numbers" plus a visits series (doc 01: honest analytics; doc 05 G5 mock).</summary>
public sealed record AnalyticsOverview(
    DateOnly From,
    DateOnly To,
    int VisitsThisWeek,
    double RepeatVisitRate,
    int ActiveMembers,
    long PointsIssued,
    long PointsRedeemed,
    int AtRiskCustomers,
    IReadOnlyList<VisitsPoint> VisitsSeries,
    Guid? TopBranchId);

public sealed record VisitsPoint(DateOnly Date, int Visits);

/// <summary>
/// Analytics computed from transactions + the points ledger (which the Transactions module owns).
/// Reporting exposes the endpoint over this contract.
/// </summary>
public interface ITransactionAnalytics
{
    Task<AnalyticsOverview> GetOverviewAsync(
        Guid merchantId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}
