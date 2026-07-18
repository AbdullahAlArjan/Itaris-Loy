using Itaris.SharedKernel;

namespace Itaris.Modules.Merchants.Domain;

/// <summary>
/// merchants.staff_roles — staff↔role, branch-scoped (doc 04 Part 8). Frozen fragments:
/// staff_member_id, branch_id (null = all branches).
/// </summary>
public sealed class StaffRole : Entity
{
    public Guid StaffMemberId { get; set; }
    public Guid RoleId { get; set; }

    /// <summary>Null = role applies across all of the merchant's branches.</summary>
    public Guid? BranchId { get; set; }
}
