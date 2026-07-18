namespace Itaris.Modules.Identity.Features.Logout;

/// <summary>doc 05 A4 body: { refreshToken } — revokes the current device's refresh family.</summary>
public sealed record LogoutRequest(string RefreshToken);
