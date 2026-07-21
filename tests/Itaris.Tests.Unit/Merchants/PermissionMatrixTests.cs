using Itaris.Modules.Merchants.Domain;

namespace Itaris.Tests.Unit.Merchants;

/// <summary>
/// Locks the seeded role → permission mapping against doc 01 Part 3's permission matrix.
/// Auth is security-critical: a silent change to any role's grants must fail a test.
/// </summary>
public class PermissionMatrixTests
{
    [Fact]
    public void Every_role_in_the_matrix_has_a_template()
    {
        string[] expectedRoles =
        [
            SystemRoles.Owner, SystemRoles.Admin, SystemRoles.BranchManager,
            SystemRoles.Cashier, SystemRoles.Marketing, SystemRoles.Analyst,
        ];

        Assert.Equal(expectedRoles.Order(), SystemRoles.Templates.Keys.Order());
    }

    [Fact]
    public void Every_granted_permission_exists_in_the_catalog()
    {
        foreach (var (role, perms) in SystemRoles.Templates)
        {
            foreach (var perm in perms)
            {
                Assert.True(Permissions.Catalog.ContainsKey(perm),
                    $"Role '{role}' grants unknown permission '{perm}'.");
            }
        }
    }

    [Theory]
    // Cashier: identify + record + refund (within limit) + confirm redemption. Nothing else.
    [InlineData(SystemRoles.Cashier, Permissions.CustomersIdentify, true)]
    [InlineData(SystemRoles.Cashier, Permissions.TransactionsRecord, true)]
    [InlineData(SystemRoles.Cashier, Permissions.RefundsCreate, true)]
    [InlineData(SystemRoles.Cashier, Permissions.RedemptionsConfirm, true)]
    [InlineData(SystemRoles.Cashier, Permissions.StaffManage, false)]
    [InlineData(SystemRoles.Cashier, Permissions.RefundsApprove, false)]
    [InlineData(SystemRoles.Cashier, Permissions.AnalyticsView, false)]
    [InlineData(SystemRoles.Cashier, Permissions.MerchantManage, false)]
    // Branch manager: wider than cashier (approve refunds, analytics, audit) but no staff/merchant mgmt.
    [InlineData(SystemRoles.BranchManager, Permissions.RefundsApprove, true)]
    [InlineData(SystemRoles.BranchManager, Permissions.AnalyticsView, true)]
    [InlineData(SystemRoles.BranchManager, Permissions.AuditView, true)]
    [InlineData(SystemRoles.BranchManager, Permissions.StaffManage, false)]
    [InlineData(SystemRoles.BranchManager, Permissions.MerchantManage, false)]
    // Owner: everything, including subscription/billing.
    [InlineData(SystemRoles.Owner, Permissions.StaffManage, true)]
    [InlineData(SystemRoles.Owner, Permissions.SubscriptionManage, true)]
    [InlineData(SystemRoles.Owner, Permissions.MerchantManage, true)]
    // Merchant admin: owner's delegate minus subscription/billing.
    [InlineData(SystemRoles.Admin, Permissions.StaffManage, true)]
    [InlineData(SystemRoles.Admin, Permissions.SubscriptionManage, false)]
    // Marketing: reward content only.
    [InlineData(SystemRoles.Marketing, Permissions.RewardsManage, true)]
    [InlineData(SystemRoles.Marketing, Permissions.TransactionsRecord, false)]
    [InlineData(SystemRoles.Marketing, Permissions.RefundsCreate, false)]
    // Analyst: read-only analytics.
    [InlineData(SystemRoles.Analyst, Permissions.AnalyticsView, true)]
    [InlineData(SystemRoles.Analyst, Permissions.TransactionsRecord, false)]
    [InlineData(SystemRoles.Analyst, Permissions.StaffManage, false)]
    public void Role_grants_permission_iff_matrix_says_so(string role, string permission, bool expected)
    {
        var granted = SystemRoles.Templates[role].Contains(permission);
        Assert.Equal(expected, granted);
    }

    [Fact]
    public void Only_owner_can_manage_subscription()
    {
        foreach (var (role, perms) in SystemRoles.Templates)
        {
            var canManageSubscription = perms.Contains(Permissions.SubscriptionManage);
            Assert.Equal(role == SystemRoles.Owner, canManageSubscription);
        }
    }
}
