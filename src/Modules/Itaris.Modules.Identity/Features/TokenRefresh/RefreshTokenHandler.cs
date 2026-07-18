using Itaris.Infrastructure.Auth;
using Itaris.Modules.Identity.Features.Shared;
using Itaris.Modules.Identity.Persistence;
using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Identity.Features.TokenRefresh;

/// <summary>
/// doc 05 A3 — rotates a refresh token. Presenting an already-consumed token is treated as
/// theft: the entire token family is revoked and TOKEN_REUSE_DETECTED is returned. Unknown,
/// revoked, or expired tokens return UNAUTHORIZED.
/// </summary>
public sealed class RefreshTokenHandler(
    IdentityDbContext db, ITokenService tokens, AuthTokenIssuer issuer, IClock clock)
{
    public async Task<Result<RefreshTokenResponse>> HandleAsync(
        RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var hash = tokens.HashRefreshSecret(request.RefreshToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (token is null || token.RevokedAt is not null)
        {
            return Fail(ErrorCodes.Unauthorized, "Invalid refresh token.");
        }

        if (token.ConsumedAt is not null)
        {
            // Reuse of a rotated token — revoke the whole family (doc 05 A3).
            await db.RefreshTokens
                .Where(t => t.FamilyId == token.FamilyId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, clock.UtcNow), cancellationToken);
            return Fail(ErrorCodes.TokenReuseDetected, "Refresh token reuse detected; session revoked.");
        }

        if (clock.UtcNow >= token.ExpiresAt)
        {
            return Fail(ErrorCodes.Unauthorized, "Refresh token expired.");
        }

        token.ConsumedAt = clock.UtcNow;
        var claims = AuthTokenIssuer.DeserializeClaims(token.UserId, token.ClaimsJson);
        var issued = issuer.Rotate(claims, token.DeviceId, token.FamilyId);

        await db.SaveChangesAsync(cancellationToken);

        return Result<RefreshTokenResponse>.Success(
            new RefreshTokenResponse(issued.AccessToken, issued.RefreshToken, issued.ExpiresInSeconds));
    }

    private static Result<RefreshTokenResponse> Fail(string code, string message) =>
        Result<RefreshTokenResponse>.Failure(code, message);
}
