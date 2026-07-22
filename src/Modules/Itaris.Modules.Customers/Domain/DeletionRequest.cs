using Itaris.SharedKernel;

namespace Itaris.Modules.Customers.Domain;

/// <summary>
/// customers.deletion_requests — PDPL account-deletion request (Jordan Law No. 24 of 2023; doc 01
/// puts deletion + privacy controls in the MVP). A request enters a 7-day grace period during which
/// the customer can cancel; after that a purge job executes it (data anonymization is a follow-up).
/// </summary>
public sealed class DeletionRequest : Entity
{
    public Guid UserId { get; set; }
    public DateTimeOffset RequestedAt { get; set; }

    /// <summary>End of the 7-day grace period; the purge runs on/after this.</summary>
    public DateTimeOffset ExecuteAfter { get; set; }

    public string Status { get; set; } = DeletionStatuses.Pending;
    public DateTimeOffset? CancelledAt { get; set; }
    public DateTimeOffset? ExecutedAt { get; set; }
}

public static class DeletionStatuses
{
    public const string Pending = "pending";
    public const string Cancelled = "cancelled";
    public const string Executed = "executed";
}
