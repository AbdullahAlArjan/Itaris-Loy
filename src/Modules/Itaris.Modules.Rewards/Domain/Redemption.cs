using Itaris.SharedKernel;

namespace Itaris.Modules.Rewards.Domain;

/// <summary>
/// rewards.redemptions — two-phase redemption (doc 04 Part 8). Frozen fragments: membership_id,
/// reward_id, status (pending/completed/cancelled/expired), code char(6), points_held bigint,
/// created_at, expires_at, confirmed_at, idempotency.
///
/// Model (doc 06 freeze: intent → confirm, 5-min TTL, online-only confirm): the intent HOLDS the
/// cost (points deducted / stamp card consumed / stock decremented) and issues a code. Confirm
/// finalizes; cancel or TTL-expiry releases the hold. The pending→completed transition is guarded
/// by a row lock so exactly one confirm of a code can win (the double-redemption defense).
/// </summary>
public sealed class Redemption : Entity
{
    public Guid MembershipId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid MerchantId { get; set; }
    public Guid RewardId { get; set; }

    public string Status { get; set; } = RedemptionStatuses.Pending;

    /// <summary>6-char human code the cashier enters (doc 05 §9.8 "K7M3QD").</summary>
    public required string Code { get; set; }

    /// <summary>Points held for this redemption (0 for a stamp reward).</summary>
    public long PointsHeld { get; set; }

    /// <summary>True when a stamp card was consumed for this redemption.</summary>
    public bool StampCardConsumed { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }

    public Guid? ConfirmedByStaffId { get; set; }
}

public static class RedemptionStatuses
{
    public const string Pending = "pending";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
    public const string Expired = "expired";
}
