using Itaris.SharedKernel;

namespace Itaris.Modules.Customers.Domain;

/// <summary>
/// customers.customer_profiles — customer-specific data incl. shadow profiles (doc 04 Part 8).
/// Frozen fragments: user_id (=users.id or standalone for shadow), first_name, gender,
/// preferred_language, is_shadow, claimed_at.
///
/// Shadow profiles are created by a cashier enrolling a phone-only customer (doc 01: phone-number
/// fallback is a first-class flow). When that person later registers with the same phone, the
/// profile is claimed (is_shadow → false) — their counter-earned memberships carry over because
/// the identity user is keyed by phone, so no record merge is needed.
/// </summary>
public sealed class CustomerProfile : Entity
{
    /// <summary>identity.users.id (no cross-schema FK — module boundary is the contract).</summary>
    public Guid UserId { get; set; }

    /// <summary>E.164; kept here for cashier phone lookup (doc 05 D2).</summary>
    public required string PhoneNumber { get; set; }

    public string? FirstName { get; set; }
    public string? Gender { get; set; }
    public string PreferredLanguage { get; set; } = "ar";
    public DateOnly? BirthDate { get; set; }

    /// <summary>True while the customer exists only as a cashier-enrolled phone (no app registration yet).</summary>
    public bool IsShadow { get; set; }

    /// <summary>Set when a shadow profile is claimed on first real registration.</summary>
    public DateTimeOffset? ClaimedAt { get; set; }
}
