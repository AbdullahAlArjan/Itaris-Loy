using Itaris.SharedKernel;

namespace Itaris.Modules.Identity.Domain;

/// <summary>
/// identity.users — all human identities (customers, staff, owners, admins), doc 04 Part 8.
/// NOTE: doc 04's table listing is visually clipped in the provided PDF; columns below marked
/// PLACEHOLDER are completions of truncated names, cross-checked against doc 05 behavior.
/// Reconcile against the un-clipped BAC before Phase 2 builds real auth on top.
/// </summary>
public sealed class User : Entity
{
    /// <summary>Frozen fragment "user_type … staff/platfo…": customer | staff | owner | platform_admin.</summary>
    public required string UserType { get; set; }

    /// <summary>Frozen fragment "phone_nu…"; E.164. Null for email-credentialed accounts per fragment "null for em…".</summary>
    public string? PhoneNumber { get; set; }

    // PLACEHOLDER(doc04-Part8-clipped): "password…" fragment — assumed password_hash, null for
    // OTP-only customers. Owner/staff credentials are Phase 2; column reserved now.
    public string? PasswordHash { get; set; }

    // PLACEHOLDER(doc04-Part8-clipped): email not visible in the clipped cell, but doc 05 A6
    // (owner login) authenticates with { email, password }, so the identity row needs one.
    public string? Email { get; set; }

    /// <summary>active | inactive | locked. Owner-login lockout (doc 05 A6) rides on these two fields.</summary>
    public string Status { get; set; } = UserStatuses.Active;

    /// <summary>Consecutive failed password attempts (owner login). Reset to 0 on success.</summary>
    public int FailedLoginAttempts { get; set; }

    /// <summary>Set when lockout trips (doc 05 A6: 5 fails / 15 min → ACCOUNT_LOCKED).</summary>
    public DateTimeOffset? LockedUntil { get; set; }
}

public static class UserTypes
{
    public const string Customer = "customer";
    public const string Staff = "staff";
    public const string Owner = "owner";
    public const string PlatformAdmin = "platform_admin";
}

public static class UserStatuses
{
    public const string Active = "active";
    public const string Inactive = "inactive";
    public const string Locked = "locked";
}
