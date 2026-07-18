using Itaris.Infrastructure.Auth;
using Itaris.Modules.Identity.PublicApi;
using Itaris.Modules.Merchants.Domain;
using Itaris.Modules.Merchants.Persistence;
using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Merchants.Features.Access;

/// <summary>
/// doc 05 A8 — invited staff accepts and sets a PIN. Validates the invite token, provisions the
/// staff identity user, stores the PIN hash, activates the staff_member, and issues a token pair.
/// </summary>
public sealed class AcceptInviteHandler(
    MerchantsDbContext db,
    IUserDirectory users,
    ITokenIssuer tokens,
    ISecretHasher hasher,
    MerchantClaimsResolver claims,
    IClock clock)
{
    public async Task<Result<AuthTokensResponse>> HandleAsync(
        AcceptInviteRequest request, CancellationToken cancellationToken)
    {
        var hash = InviteStaffHandler.Hash(request.InviteToken);
        var invite = await db.StaffInvites.FirstOrDefaultAsync(i => i.TokenHash == hash, cancellationToken);

        if (invite is null || invite.AcceptedAt is not null || clock.UtcNow >= invite.ExpiresAt)
        {
            return Fail(ErrorCodes.ValidationError, "Invalid or expired invite.");
        }

        var staff = await db.StaffMembers.FirstAsync(s => s.Id == invite.StaffMemberId, cancellationToken);

        var userId = await users.CreateStaffUserAsync(staff.PhoneOrEmail, cancellationToken);

        staff.UserId = userId;
        staff.PinHash = hasher.Hash(request.Pin);
        staff.Status = StaffStatuses.Active;
        invite.AcceptedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var resolved = await claims.ResolveAsync(staff, cancellationToken);
        var merchant = await db.Merchants.FirstAsync(m => m.Id == staff.MerchantId, cancellationToken);

        var pair = await tokens.IssueAsync(
            new TokenRequest(userId, Audience: "staff",
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
