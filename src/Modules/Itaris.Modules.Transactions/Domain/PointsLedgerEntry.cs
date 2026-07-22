using Itaris.SharedKernel;

namespace Itaris.Modules.Transactions.Domain;

/// <summary>
/// transactions.points_ledger_entries — the immutable source of truth for points/stamps
/// (doc 04 Part 8). Frozen fragments: id uuid (v7 = chronological), membership_id, entry_type
/// (earn/refund_reversal/adjustment/welcome_bonus/redeem…), points_delta, stamps_delta,
/// balance_after (denormalized), source_type (transaction/redemption/…), reason, created_by.
///
/// Invariants (doc 04): append-only; every entry references its cause; sum of deltas = balance,
/// always — enforced by writing the entry and the membership projection in one DB transaction
/// under a row lock on the membership.
/// </summary>
public sealed class PointsLedgerEntry : Entity
{
    public Guid MembershipId { get; set; }

    /// <summary>earn | refund_reversal | adjustment | welcome_bonus | redeem.</summary>
    public required string EntryType { get; set; }

    public long PointsDelta { get; set; }
    public int StampsDelta { get; set; }

    /// <summary>Points balance after applying this entry (denormalized check value).</summary>
    public long BalanceAfter { get; set; }

    /// <summary>transaction | refund | redemption | adjustment.</summary>
    public required string SourceType { get; set; }

    public Guid SourceId { get; set; }

    public string? Reason { get; set; }

    /// <summary>Staff/user who caused the entry.</summary>
    public Guid? CreatedBy { get; set; }
}

public static class LedgerEntryTypes
{
    public const string Earn = "earn";
    public const string RefundReversal = "refund_reversal";
    public const string Adjustment = "adjustment";
    public const string WelcomeBonus = "welcome_bonus";
    public const string Redeem = "redeem";
}

public static class LedgerSourceTypes
{
    public const string Transaction = "transaction";
    public const string Refund = "refund";
    public const string Redemption = "redemption";
    public const string Adjustment = "adjustment";
}
