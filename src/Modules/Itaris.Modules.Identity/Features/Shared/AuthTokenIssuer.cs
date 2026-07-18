using System.Text.Json;
using Itaris.Infrastructure.Auth;
using Itaris.Modules.Identity.Domain;
using Itaris.Modules.Identity.Persistence;
using Itaris.SharedKernel;
using Microsoft.Extensions.Options;

namespace Itaris.Modules.Identity.Features.Shared;

public sealed record IssuedTokens(string AccessToken, int ExpiresInSeconds, string RefreshToken);

/// <summary>
/// Issues an access token + a persisted rotating refresh token. A new login starts a fresh
/// token family; <see cref="Rotate"/> keeps the family so reuse detection can revoke it.
/// The access-token claims are snapshotted onto the refresh row so rotation never calls other
/// modules. Callers own their SaveChanges scope; this only stages rows.
/// </summary>
public sealed class AuthTokenIssuer(IdentityDbContext db, ITokenService tokens, IClock clock, IOptions<JwtOptions> jwtOptions)
{
    public IssuedTokens Issue(TokenRequest request, Guid deviceId) =>
        Create(request, deviceId, Uuid.NewV7());

    public IssuedTokens Rotate(TokenRequest request, Guid deviceId, Guid familyId) =>
        Create(request, deviceId, familyId);

    /// <summary>Rebuilds a TokenRequest from a stored claims snapshot for a given user.</summary>
    public static TokenRequest DeserializeClaims(Guid userId, string claimsJson)
    {
        var snapshot = JsonSerializer.Deserialize<TokenRequest>(claimsJson)!;
        return snapshot with { UserId = userId };
    }

    private IssuedTokens Create(TokenRequest request, Guid deviceId, Guid familyId)
    {
        var access = tokens.CreateAccessToken(request);
        var secret = tokens.GenerateRefreshSecret();

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = request.UserId,
            DeviceId = deviceId,
            TokenHash = tokens.HashRefreshSecret(secret),
            ClaimsJson = JsonSerializer.Serialize(request),
            FamilyId = familyId,
            ExpiresAt = clock.UtcNow.AddDays(jwtOptions.Value.RefreshTokenDays),
        });

        return new IssuedTokens(access.Token, access.ExpiresInSeconds, secret);
    }
}
