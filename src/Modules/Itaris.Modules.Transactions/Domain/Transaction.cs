using Itaris.SharedKernel;

namespace Itaris.Modules.Transactions.Domain;

/// <summary>
/// transactions.transactions — sales (doc 04 Part 8). Frozen fragments: merchant_id,
/// membership_id, staff_member_id, (branch/POS), amount bigint, currency, note, status
/// (completed/refunded/partially_refunded), occurred_at, recorded_at, source (cashier/sync), rule.
/// </summary>
public sealed class Transaction : Entity
{
    public Guid MerchantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid MembershipId { get; set; }
    public Guid StaffMemberId { get; set; }

    public long AmountMinor { get; set; }
    public string Currency { get; set; } = Money.Jod;
    public string? Note { get; set; }

    public string Status { get; set; } = TransactionStatuses.Completed;

    /// <summary>When the sale physically happened (cashier clock, esp. for offline sync).</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>When the platform recorded it.</summary>
    public DateTimeOffset RecordedAt { get; set; }

    /// <summary>cashier | sync (doc 04 "source").</summary>
    public string Source { get; set; } = TransactionSources.Cashier;

    /// <summary>The loyalty rule version the earn was computed under (doc 04 "rule" fragment).</summary>
    public Guid? RuleId { get; set; }

    /// <summary>Cumulative refunded amount (fils) across all refunds of this transaction.</summary>
    public long RefundedAmountMinor { get; set; }
}

public static class TransactionStatuses
{
    public const string Completed = "completed";
    public const string PartiallyRefunded = "partially_refunded";
    public const string Refunded = "refunded";
}

public static class TransactionSources
{
    public const string Cashier = "cashier";
    public const string Sync = "sync";
}
