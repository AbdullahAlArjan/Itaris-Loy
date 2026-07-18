using Itaris.SharedKernel;

namespace Itaris.Modules.Merchants.Domain;

/// <summary>
/// merchants.staff_members — employment link user↔merchant (doc 04 Part 8). Frozen fragments:
/// merchant_id, display_name, status (invited/active/locked/removed), refund_limit (nullable
/// override). PIN auth (doc 05 A7) lives here: pin_hash + lockout counters.
/// UserId links to identity.users by id (no cross-schema FK — module boundary is the contract).
/// </summary>
public sealed class StaffMember : Entity
{
    public Guid MerchantId { get; set; }

    /// <summary>identity.users.id once the invite is accepted; null while pending.</summary>
    public Guid? UserId { get; set; }

    public required string DisplayName { get; set; }
    public string? PhoneOrEmail { get; set; }
    public string Status { get; set; } = StaffStatuses.Invited;

    /// <summary>Per-staff refund ceiling override (fils); null = inherit merchant setting.</summary>
    public long? RefundLimitMinor { get; set; }

    public string? PinHash { get; set; }
    public int FailedPinAttempts { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
}

public static class StaffStatuses
{
    public const string Invited = "invited";
    public const string Active = "active";
    public const string Locked = "locked";
    public const string Removed = "removed";
}
