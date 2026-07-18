using Itaris.Infrastructure.Auth;
using Itaris.Modules.Identity.PublicApi;
using Itaris.Modules.Merchants.Persistence;
using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Merchants.Features.Access;

/// <summary>
/// doc 05 A6 — owner email/password login. Verifies credentials via the Identity directory
/// (which tracks lockout: 5 fails / 15 min → ACCOUNT_LOCKED), resolves the owner's merchant
/// claims, and issues a token pair. Errors: INVALID_CREDENTIALS, ACCOUNT_LOCKED.
/// </summary>
public sealed class OwnerLoginHandler(
    MerchantsDbContext db, IUserDirectory users, ITokenIssuer tokens, MerchantClaimsResolver claims)
{
    public async Task<Result<AuthTokensResponse>> HandleAsync(
        OwnerLoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var verify = await users.VerifyOwnerAsync(email, request.Password, cancellationToken);

        switch (verify.Status)
        {
            case OwnerVerifyStatus.Locked:
                return Fail(ErrorCodes.AccountLocked, "Account locked. Try again later.");
            case OwnerVerifyStatus.Invalid:
                return Fail(ErrorCodes.InvalidCredentials, "Invalid email or password.");
        }

        var resolved = await claims.ResolveByUserAsync(verify.UserId!.Value, cancellationToken);
        if (resolved is null)
        {
            return Fail(ErrorCodes.InvalidCredentials, "No active merchant membership for this owner.");
        }

        var merchant = await db.Merchants
            .FirstAsync(m => m.Id == resolved.MerchantId, cancellationToken);

        var tokenRequest = new TokenRequest(
            verify.UserId.Value, Audience: "staff",
            MerchantId: resolved.MerchantId, StaffId: resolved.StaffId, Role: resolved.Role,
            BranchIds: resolved.BranchIds, Permissions: resolved.Permissions);

        var pair = await tokens.IssueAsync(tokenRequest, request.Device, cancellationToken);

        return Result<AuthTokensResponse>.Success(new AuthTokensResponse(
            pair.AccessToken, pair.RefreshToken, pair.ExpiresIn,
            new MerchantSummary(merchant.Id, merchant.Code, merchant.NameEn)));
    }

    private static Result<AuthTokensResponse> Fail(string code, string message) =>
        Result<AuthTokensResponse>.Failure(code, message);
}
