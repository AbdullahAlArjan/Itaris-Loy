using Itaris.SharedKernel;

namespace Itaris.Modules.Identity.Domain;

/// <summary>
/// identity.refresh_tokens — rotating refresh tokens, doc 04 Part 8. Frozen fragments:
/// user_id, d… (device), token_has…, expires_a…, revoked_a…, "(per-user family rotatable)".
/// Family semantics per doc 05 A3: reuse of a consumed token revokes the whole family
/// (TOKEN_REUSE_DETECTED); A4 logout revokes the current device's family.
/// </summary>
public sealed class RefreshToken : Entity
{
    public Guid UserId { get; set; }

    public Guid DeviceId { get; set; }

    public required string TokenHash { get; set; }

    /// <summary>
    /// Serialized snapshot of the access-token claims (audience, merchant/staff/branch/permissions)
    /// so rotation can remint without calling other modules. A denormalized auth cache owned by
    /// Identity; permission changes take effect on next full login (bounded staleness, documented).
    /// </summary>
    public required string ClaimsJson { get; set; }

    /// <summary>Rotation family id; every rotation keeps the family, reuse detection revokes by it.</summary>
    public Guid FamilyId { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
}
