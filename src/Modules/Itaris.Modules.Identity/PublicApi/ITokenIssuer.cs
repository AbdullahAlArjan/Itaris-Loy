using Itaris.Infrastructure.Auth;

namespace Itaris.Modules.Identity.PublicApi;

/// <summary>Device details supplied at login (doc 05 A2/A7 device object).</summary>
public sealed record DeviceRegistration(string Platform, string? Model, string? FcmToken);

public sealed record TokenPair(string AccessToken, string RefreshToken, int ExpiresIn);

/// <summary>
/// Public token-issuing surface of the Identity module. Other modules (e.g. Merchants staff/owner
/// login) mint tokens through this instead of touching identity's tables. Registers the device
/// and persists the rotating refresh token in the identity schema.
/// </summary>
public interface ITokenIssuer
{
    Task<TokenPair> IssueAsync(TokenRequest request, DeviceRegistration device, CancellationToken cancellationToken);
}
