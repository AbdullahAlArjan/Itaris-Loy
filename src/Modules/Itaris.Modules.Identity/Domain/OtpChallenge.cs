using Itaris.SharedKernel;

namespace Itaris.Modules.Identity.Domain;

/// <summary>
/// identity.otp_challenges — OTP lifecycle, doc 04 Part 8. Frozen fragments:
/// phone_nu…, code_has…, attempts, expires_a…, consumed….
/// </summary>
public sealed class OtpChallenge : Entity
{
    public required string PhoneNumber { get; set; }

    /// <summary>Hash of the OTP code — the plaintext code is never stored (doc 04 security posture).</summary>
    public required string CodeHash { get; set; }

    /// <summary>doc 05 A1 request body carries purpose: "login".</summary>
    public required string Purpose { get; set; }

    public int Attempts { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }
}
