using Itaris.Infrastructure.Auth;
using Itaris.Modules.Identity.Persistence;
using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Identity.Features.Logout;

/// <summary>
/// doc 05 A4 — revokes the refresh family of the presented token (the current device's session).
/// Idempotent: an unknown token is a no-op success so logout never fails noisily.
/// </summary>
public sealed class LogoutHandler(IdentityDbContext db, ITokenService tokens, IClock clock)
{
    public async Task<Result<bool>> HandleAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        var hash = tokens.HashRefreshSecret(request.RefreshToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (token is not null)
        {
            await db.RefreshTokens
                .Where(t => t.FamilyId == token.FamilyId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, clock.UtcNow), cancellationToken);
        }

        return Result<bool>.Success(true);
    }
}
