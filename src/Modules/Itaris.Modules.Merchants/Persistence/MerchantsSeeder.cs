using Itaris.Modules.Merchants.Domain;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Merchants.Persistence;

/// <summary>
/// Idempotent seed of the permission catalog, system role templates, and their mappings
/// (doc 04: roles/permissions are seeded). Uses deterministic IDs so re-runs upsert cleanly.
/// </summary>
public static class MerchantsSeeder
{
    public static async Task SeedAsync(MerchantsDbContext db, CancellationToken cancellationToken = default)
    {
        var existingPerms = await db.Permissions.Select(p => p.Code).ToListAsync(cancellationToken);
        foreach (var (code, description) in Permissions.Catalog)
        {
            if (!existingPerms.Contains(code))
            {
                db.Permissions.Add(new Permission
                {
                    Id = DeterministicGuid.Create($"permission:{code}"),
                    Code = code,
                    Description = description,
                });
            }
        }

        var existingRoles = await db.Roles
            .Where(r => r.MerchantId == null)
            .Select(r => r.Name)
            .ToListAsync(cancellationToken);
        foreach (var roleName in SystemRoles.Templates.Keys)
        {
            if (!existingRoles.Contains(roleName))
            {
                db.Roles.Add(new Role
                {
                    Id = DeterministicGuid.Create($"role:{roleName}"),
                    MerchantId = null,
                    Name = roleName,
                    IsSystem = true,
                });
            }
        }

        var existingMappings = await db.RolePermissions
            .Select(rp => new { rp.RoleId, rp.PermissionId })
            .ToListAsync(cancellationToken);
        foreach (var (roleName, perms) in SystemRoles.Templates)
        {
            var roleId = DeterministicGuid.Create($"role:{roleName}");
            foreach (var permCode in perms)
            {
                var permId = DeterministicGuid.Create($"permission:{permCode}");
                if (!existingMappings.Any(m => m.RoleId == roleId && m.PermissionId == permId))
                {
                    db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permId });
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
