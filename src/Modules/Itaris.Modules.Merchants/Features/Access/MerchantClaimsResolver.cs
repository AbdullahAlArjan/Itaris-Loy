using Itaris.Modules.Merchants.Domain;
using Itaris.Modules.Merchants.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Merchants.Features.Access;

/// <summary>
/// Resolves the permission strings, role, and branch scope for a staff member (owner included) —
/// the claims that go into their access token. Empty BranchIds means "all branches" (owner/admin).
/// </summary>
public sealed record StaffClaims(
    Guid MerchantId, Guid StaffId, string? Role,
    IReadOnlyList<Guid> BranchIds, IReadOnlyList<string> Permissions);

public sealed class MerchantClaimsResolver(MerchantsDbContext db)
{
    public async Task<StaffClaims?> ResolveByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var staff = await db.StaffMembers.FirstOrDefaultAsync(
            s => s.UserId == userId && s.Status == StaffStatuses.Active, cancellationToken);
        return staff is null ? null : await ResolveAsync(staff, cancellationToken);
    }

    public async Task<StaffClaims> ResolveAsync(StaffMember staff, CancellationToken cancellationToken)
    {
        var staffRoles = await db.StaffRoles
            .Where(sr => sr.StaffMemberId == staff.Id)
            .ToListAsync(cancellationToken);

        var roleIds = staffRoles.Select(sr => sr.RoleId).Distinct().ToList();

        var permissions = await (
            from rp in db.RolePermissions
            join p in db.Permissions on rp.PermissionId equals p.Id
            where roleIds.Contains(rp.RoleId)
            select p.Code).Distinct().ToListAsync(cancellationToken);

        var primaryRole = await db.Roles
            .Where(r => roleIds.Contains(r.Id))
            .Select(r => r.Name)
            .FirstOrDefaultAsync(cancellationToken);

        var branchIds = staffRoles
            .Where(sr => sr.BranchId is not null)
            .Select(sr => sr.BranchId!.Value)
            .Distinct()
            .ToList();

        return new StaffClaims(staff.MerchantId, staff.Id, primaryRole, branchIds, permissions);
    }
}
