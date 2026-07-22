using Itaris.SharedKernel;

namespace Itaris.Modules.Transactions.Domain;

/// <summary>
/// transactions.refunds — full/partial refunds (doc 04 Part 8). Frozen fragments: transaction_id,
/// type (full/partial), amount_minor, points_clawback, stamps_clawback, reason, requested_by,
/// approved_by (nullable).
/// </summary>
public sealed class Refund : Entity
{
    public Guid TransactionId { get; set; }

    /// <summary>full | partial.</summary>
    public required string Type { get; set; }

    public long AmountMinor { get; set; }

    public long PointsClawback { get; set; }
    public int StampsClawback { get; set; }

    public string? Reason { get; set; }

    public Guid RequestedBy { get; set; }
    public Guid? ApprovedBy { get; set; }
}

public static class RefundTypes
{
    public const string Full = "full";
    public const string Partial = "partial";
}
