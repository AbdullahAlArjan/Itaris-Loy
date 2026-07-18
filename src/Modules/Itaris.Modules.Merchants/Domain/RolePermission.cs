namespace Itaris.Modules.Merchants.Domain;

/// <summary>merchants.role_permissions — role↔permission map (doc 04 Part 8). Composite PK (role_id, permission_id).</summary>
public sealed class RolePermission
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }
}
