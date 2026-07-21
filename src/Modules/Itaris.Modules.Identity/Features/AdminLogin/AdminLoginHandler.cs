using Itaris.Infrastructure.Auth;
using Itaris.Modules.Identity.Features.Shared;
using Itaris.Modules.Identity.PublicApi;
using Itaris.SharedKernel;

namespace Itaris.Modules.Identity.Features.AdminLogin;

/// <summary>
/// Platform-admin login. Issues an "admin"-audience token carrying the platform permission that
/// gates admin-only endpoints (e.g. merchant creation, doc 05 C10).
/// Errors: INVALID_CREDENTIALS, ACCOUNT_LOCKED.
/// </summary>
public sealed class AdminLoginHandler(IUserDirectory users, ITokenIssuer tokens)
{
    /// <summary>Permissions granted to platform admins; gate the /v1/admin surface.</summary>
    public const string AdminCreateMerchantPermission = "admin.merchants.create";
    public const string AdminManageMerchantPermission = "admin.merchants.manage";

    public async Task<Result<AdminLoginResponse>> HandleAsync(
        AdminLoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var verify = await users.VerifyAdminAsync(email, request.Password, cancellationToken);

        if (verify.Status == OwnerVerifyStatus.Locked)
        {
            return Result<AdminLoginResponse>.Failure(ErrorCodes.AccountLocked, "Account locked. Try again later.");
        }

        if (verify.Status != OwnerVerifyStatus.Ok)
        {
            return Result<AdminLoginResponse>.Failure(ErrorCodes.InvalidCredentials, "Invalid email or password.");
        }

        var pair = await tokens.IssueAsync(
            new TokenRequest(verify.UserId!.Value, Audience: "admin",
                Permissions: [AdminCreateMerchantPermission, AdminManageMerchantPermission]),
            request.Device, cancellationToken);

        return Result<AdminLoginResponse>.Success(
            new AdminLoginResponse(pair.AccessToken, pair.RefreshToken, pair.ExpiresIn));
    }
}
