using System.Security.Cryptography;
using Itaris.Infrastructure.Auth;
using Itaris.Modules.Merchants.Domain;
using Itaris.Modules.Merchants.Persistence;
using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Merchants.Features.Access;

/// <summary>
/// doc 05 C7 — owner/admin invites a staff member. Creates the (invited) staff_member, assigns
/// the requested system role (optionally branch-scoped), and returns a one-time invite token
/// (only its hash is stored). Managers may invite cashiers only; that policy check is enforced
/// at the endpoint via permissions. Errors: ALREADY_STAFF.
/// </summary>
public sealed class InviteStaffHandler(MerchantsDbContext db, ITokenService tokens, IClock clock)
{
    public async Task<Result<InviteStaffResponse>> HandleAsync(
        Guid merchantId, InviteStaffRequest request, CancellationToken cancellationToken)
    {
        if (!SystemRoles.Templates.ContainsKey(request.Role))
        {
            return Fail(ErrorCodes.ValidationError, $"Unknown role '{request.Role}'.");
        }

        var contact = request.PhoneOrEmail.Trim().ToLowerInvariant();
        var exists = await db.StaffMembers.AnyAsync(
            s => s.MerchantId == merchantId && s.PhoneOrEmail == contact && s.Status != StaffStatuses.Removed,
            cancellationToken);
        if (exists)
        {
            return Fail(ErrorCodes.AlreadyStaff, "This person is already a staff member.");
        }

        var staff = new StaffMember
        {
            MerchantId = merchantId,
            DisplayName = request.DisplayName,
            PhoneOrEmail = contact,
            Status = StaffStatuses.Invited,
        };
        db.StaffMembers.Add(staff);

        db.StaffRoles.Add(new StaffRole
        {
            StaffMemberId = staff.Id,
            RoleId = DeterministicGuid.Create($"role:{request.Role}"),
            BranchId = request.BranchId,
        });

        var inviteSecret = tokens.GenerateRefreshSecret();
        db.StaffInvites.Add(new StaffInvite
        {
            MerchantId = merchantId,
            StaffMemberId = staff.Id,
            TokenHash = Hash(inviteSecret),
            ExpiresAt = clock.UtcNow.AddDays(7),
        });

        await db.SaveChangesAsync(cancellationToken);

        return Result<InviteStaffResponse>.Success(new InviteStaffResponse(staff.Id, inviteSecret));
    }

    internal static string Hash(string secret) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret)));

    private static Result<InviteStaffResponse> Fail(string code, string message) =>
        Result<InviteStaffResponse>.Failure(code, message);
}
