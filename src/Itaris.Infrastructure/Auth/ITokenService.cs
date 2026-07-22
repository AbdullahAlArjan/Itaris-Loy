namespace Itaris.Infrastructure.Auth;

/// <summary>
/// Issues signed access tokens and opaque refresh-token secrets. The Identity module owns
/// refresh-token persistence, rotation, and reuse detection; this service only mints strings.
/// </summary>
public interface ITokenService
{
    AccessToken CreateAccessToken(TokenRequest request);

    /// <summary>Cryptographically-random opaque refresh secret. Store only its hash.</summary>
    string GenerateRefreshSecret();

    /// <summary>Stable hash of a refresh secret for storage/lookup.</summary>
    string HashRefreshSecret(string secret);

    /// <summary>
    /// A short-lived signed blob identifying a customer, for the rotating QR (doc 05 B3, 60s).
    /// Verifiable by the cashier resolve-qr flow (Phase 4) with the same signing key.
    /// </summary>
    string CreateQrToken(Guid customerId, int ttlSeconds);

    /// <summary>Validates a QR payload; distinguishes expired from invalid (doc 05 D1 errors).</summary>
    QrValidation ValidateQrToken(string qrPayload);
}

public enum QrValidationStatus { Valid, Expired, Invalid }

/// <summary>Nonce is the token's jti — used by the resolver to enforce single use.</summary>
public sealed record QrValidation(QrValidationStatus Status, Guid CustomerId, string Nonce, DateTimeOffset ExpiresAt);
