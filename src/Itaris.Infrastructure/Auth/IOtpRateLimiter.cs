namespace Itaris.Infrastructure.Auth;

/// <summary>
/// Per-IP OTP throttling (doc 05 A1: 5/ip/h). In-memory, single-instance scale.
/// The per-phone limit (3/phone/h) is enforced in the Identity handler by counting
/// recent otp_challenges rows, since that data is authoritative in the database.
/// </summary>
public interface IOtpRateLimiter
{
    /// <summary>Records an attempt for the IP and returns false if the hourly cap is exceeded.</summary>
    bool TryConsumeForIp(string ipAddress);
}
