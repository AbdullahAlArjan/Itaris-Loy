using Itaris.Infrastructure.Auth;
using Itaris.Modules.Identity.PublicApi;
using Itaris.Modules.Merchants.Domain;
using Itaris.Modules.Merchants.Persistence;
using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Merchants.Features.Access;

/// <summary>
/// doc 05 A7 — staff PIN login. Resolves merchant by code and the active staff member by contact,
/// verifies the PIN with lockout, and issues a token pair carrying merchant/branch/permission
/// claims. Errors: INVALID_PIN (attemptsLeft), STAFF_LOCKED, STAFF_INACTIVE.
/// </summary>
public sealed class StaffLoginHandler(
    MerchantsDbContext db, ISecretHasher hasher, ITokenIssuer tokens, MerchantClaimsResolver claims, IClock clock)
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutWindow = TimeSpan.FromMinutes(15);

    public async Task<Result<AuthTokensResponse>> HandleAsync(
        StaffLoginRequest request, CancellationToken cancellationToken)
    {
        var merchant = await db.Merchants
            .FirstOrDefaultAsync(m => m.Code == request.MerchantCode, cancellationToken);
        if (merchant is null)
        {
            return Fail(ErrorCodes.InvalidPin, "Invalid credentials.");
        }

        var contact = request.PhoneOrEmail.Trim().ToLowerInvariant();
        var staff = await db.StaffMembers.FirstOrDefaultAsync(
            s => s.MerchantId == merchant.Id && s.PhoneOrEmail == contact, cancellationToken);

        if (staff is null || staff.Status == StaffStatuses.Removed)
        {
            return Fail(ErrorCodes.InvalidPin, "Invalid credentials.");
        }

        if (staff.Status == StaffStatuses.Invited || staff.PinHash is null || staff.UserId is null)
        {
            return Fail(ErrorCodes.StaffInactive, "Staff account not yet activated.");
        }

        if (staff.LockedUntil is { } lockedUntil && lockedUntil > clock.UtcNow)
        {
            return Fail(ErrorCodes.StaffLocked, "Account locked. Try again later.");
        }

        if (!hasher.Verify(staff.PinHash, request.Pin))
        {
            staff.FailedPinAttempts++;
            if (staff.FailedPinAttempts >= MaxFailedAttempts)
            {
                staff.LockedUntil = clock.UtcNow.Add(LockoutWindow);
                staff.FailedPinAttempts = 0;
                staff.Status = StaffStatuses.Locked;
                await db.SaveChangesAsync(cancellationToken);
                return Fail(ErrorCodes.StaffLocked, "Account locked after too many attempts.");
            }

            await db.SaveChangesAsync(cancellationToken);
            var attemptsLeft = MaxFailedAttempts - staff.FailedPinAttempts;
            return Result<AuthTokensResponse>.Failure(
                ErrorCodes.InvalidPin, "Incorrect PIN.",
                new Dictionary<string, object?> { ["attemptsLeft"] = attemptsLeft });
        }

        staff.FailedPinAttempts = 0;
        staff.LockedUntil = null;
        await db.SaveChangesAsync(cancellationToken);

        var resolved = await claims.ResolveAsync(staff, cancellationToken);
        var pair = await tokens.IssueAsync(
            new TokenRequest(staff.UserId.Value, Audience: "staff",
                MerchantId: resolved.MerchantId, StaffId: resolved.StaffId, Role: resolved.Role,
                BranchIds: resolved.BranchIds, Permissions: resolved.Permissions),
            request.Device, cancellationToken);

        return Result<AuthTokensResponse>.Success(new AuthTokensResponse(
            pair.AccessToken, pair.RefreshToken, pair.ExpiresIn,
            new MerchantSummary(merchant.Id, merchant.Code, merchant.NameEn)));
    }

    private static Result<AuthTokensResponse> Fail(string code, string message) =>
        Result<AuthTokensResponse>.Failure(code, message);
}
