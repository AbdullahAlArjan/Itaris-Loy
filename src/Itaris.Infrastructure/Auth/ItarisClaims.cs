namespace Itaris.Infrastructure.Auth;

/// <summary>
/// Custom JWT claim types. doc 05: three audiences (customer, staff, admin); staff tokens
/// carry { merchantId, staffId, role, branchIds, permissions[] }.
/// </summary>
public static class ItarisClaims
{
    public const string Audience = "itaris_aud";
    public const string MerchantId = "merchant_id";
    public const string StaffId = "staff_id";
    public const string BranchId = "branch_id";
    public const string Permission = "perm";
    public const string Role = "role";
}
