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
}
