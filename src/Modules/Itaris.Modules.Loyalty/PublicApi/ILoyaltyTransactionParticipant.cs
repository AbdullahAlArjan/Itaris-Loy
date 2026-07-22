using System.Data.Common;

namespace Itaris.Modules.Loyalty.PublicApi;

/// <summary>Outcome of applying an earn to a membership (one sale).</summary>
public sealed record EarnApplication(
    Guid MembershipId,
    Guid CustomerId,
    bool IsNewMember,
    string ProgramType,
    Guid RuleId,
    long PointsEarned,
    int StampsEarned,
    long NewPointsBalance,
    int StampsFilled,
    int CardSize,
    bool CardCompleted,
    int CardCycle,
    int WelcomeBonusApplied);

public sealed record ReversalApplication(long NewPointsBalance, int StampsFilled);

public enum EarnFailure { None, ProgramInactive }

public sealed record EarnOutcome(EarnApplication? Applied, EarnFailure Failure);

/// <summary>
/// Loyalty's enlisting contract for the Transactions module (doc 04: "single shared transaction …
/// Transactions orchestrates via its own context + contracts that enlist"). Implementations open a
/// Loyalty context ON THE CALLER'S connection/transaction, take a row lock on the membership
/// (SELECT … FOR UPDATE), auto-join on first transaction, apply the earn/reversal to the balance
/// projection, and return what happened — all atomic with the caller's ledger write.
/// </summary>
public interface ILoyaltyTransactionParticipant
{
    /// <summary>Applies a sale's earn. Auto-joins the customer to the merchant's active program if needed.</summary>
    Task<EarnOutcome> ApplyEarnAsync(
        Guid merchantId, Guid customerId, long amountMinor,
        DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken);

    /// <summary>Applies a refund reversal (negative deltas). Balance may go negative (doc 06).</summary>
    Task<ReversalApplication> ApplyReversalAsync(
        Guid membershipId, long pointsDelta, int stampsDelta,
        DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken);

    /// <summary>Resolves a customer's membership summary for a merchant (cashier identify flows). Read-only.</summary>
    Task<EarnApplication?> GetMembershipSnapshotAsync(
        Guid merchantId, Guid customerId, CancellationToken cancellationToken);

    /// <summary>Owner (customer id) of a membership — lets the ledger endpoint verify access. Read-only.</summary>
    Task<Guid?> GetMembershipOwnerAsync(Guid membershipId, CancellationToken cancellationToken);
}
