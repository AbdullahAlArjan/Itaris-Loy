namespace Itaris.Modules.Identity.PublicApi;

public enum OwnerVerifyStatus { Ok, Invalid, Locked }

public sealed record OwnerVerifyResult(OwnerVerifyStatus Status, Guid? UserId);

/// <summary>
/// Public identity-directory surface. Merchants uses it to provision owner/staff identity rows
/// (identity.users) and to verify owner credentials with lockout, without touching identity tables.
/// </summary>
public interface IUserDirectory
{
    Task<Guid> CreateOwnerAsync(string email, string password, CancellationToken cancellationToken);

    Task<Guid> CreateStaffUserAsync(string? phoneOrEmail, CancellationToken cancellationToken);

    /// <summary>Verifies owner email/password, tracking lockout (doc 05 A6: 5 fails / 15 min → locked).</summary>
    Task<OwnerVerifyResult> VerifyOwnerAsync(string email, string password, CancellationToken cancellationToken);

    /// <summary>Verifies a platform-admin email/password (same lockout policy as owner).</summary>
    Task<OwnerVerifyResult> VerifyAdminAsync(string email, string password, CancellationToken cancellationToken);

    /// <summary>Idempotently ensures a platform-admin user exists (startup seed). Returns its id.</summary>
    Task<Guid> EnsureAdminAsync(string email, string password, CancellationToken cancellationToken);
}
