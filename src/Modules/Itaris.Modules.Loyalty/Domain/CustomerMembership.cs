using Itaris.SharedKernel;

namespace Itaris.Modules.Loyalty.Domain;

/// <summary>
/// loyalty.customer_memberships — customer↔program link holding the cached balance + stamp state
/// (doc 04 Part 8). Frozen fragments: customer_id, merchant_id, points_balance, stamps_filled,
/// stamp_card (cycle), joined_at, join_source (app/counter).
///
/// The balance here is a PROJECTION; the immutable source of truth is points_ledger_entries
/// (transactions schema, written by the Transactions module in Phase 4). Membership is per-merchant
/// (doc 06 freeze), not per-branch. CustomerId references identity.users by id (no cross-schema FK).
/// </summary>
public sealed class CustomerMembership : Entity
{
    public Guid CustomerId { get; set; }
    public Guid MerchantId { get; set; }
    public Guid ProgramId { get; set; }

    /// <summary>Cached points balance (points programs).</summary>
    public long PointsBalance { get; set; }

    /// <summary>Stamps filled on the current card (stamp programs).</summary>
    public int StampsFilled { get; set; }

    /// <summary>How many cards have been completed (doc 04 stamp_card "cycle").</summary>
    public int StampCardCycle { get; set; }

    public DateTimeOffset JoinedAt { get; set; }

    /// <summary>app | counter (doc 04 join source; counter = cashier shadow enroll).</summary>
    public string JoinSource { get; set; } = MembershipJoinSources.App;
}

public static class MembershipJoinSources
{
    public const string App = "app";
    public const string Counter = "counter";
}
