namespace Itaris.Modules.Identity.Features.TokenRefresh;

/// <summary>doc 05 A3 body: { refreshToken } → new pair (rotation).</summary>
public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record RefreshTokenResponse(string AccessToken, string RefreshToken, int ExpiresIn);
