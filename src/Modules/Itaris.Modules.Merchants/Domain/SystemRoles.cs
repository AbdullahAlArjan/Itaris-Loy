namespace Itaris.Modules.Merchants.Domain;

/// <summary>
/// Seeded system role templates (doc 04 roles table; doc 01 Part 3 MVP roles: owner, admin,
/// branch_manager, cashier + v1.1 marketing, analyst). Each maps to a set of permission strings.
/// Business logic checks permissions, never these names.
/// </summary>
public static class SystemRoles
{
    public const string Owner = "owner";
    public const string Admin = "admin";
    public const string BranchManager = "branch_manager";
    public const string Cashier = "cashier";
    public const string Marketing = "marketing";
    public const string Analyst = "analyst";

    /// <summary>Role → permission strings (doc 01 Part 3 permission matrix).</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Templates = new Dictionary<string, string[]>
    {
        [Owner] =
        [
            Permissions.MerchantManage, Permissions.BranchesManage, Permissions.StaffManage,
            Permissions.SubscriptionManage, Permissions.ProgramsManage, Permissions.RewardsManage,
            Permissions.CustomersIdentify, Permissions.TransactionsRecord, Permissions.RefundsCreate,
            Permissions.RefundsApprove, Permissions.RedemptionsConfirm, Permissions.PointsAdjust,
            Permissions.AnalyticsView, Permissions.AuditView,
        ],
        // Merchant admin: everything except subscription/billing (owner's delegate).
        [Admin] =
        [
            Permissions.MerchantManage, Permissions.BranchesManage, Permissions.StaffManage,
            Permissions.ProgramsManage, Permissions.RewardsManage, Permissions.CustomersIdentify,
            Permissions.TransactionsRecord, Permissions.RefundsCreate, Permissions.RefundsApprove,
            Permissions.RedemptionsConfirm, Permissions.PointsAdjust, Permissions.AnalyticsView,
            Permissions.AuditView,
        ],
        [BranchManager] =
        [
            Permissions.CustomersIdentify, Permissions.TransactionsRecord, Permissions.RefundsCreate,
            Permissions.RefundsApprove, Permissions.RedemptionsConfirm, Permissions.AnalyticsView,
            Permissions.AuditView,
        ],
        [Cashier] =
        [
            Permissions.CustomersIdentify, Permissions.TransactionsRecord, Permissions.RefundsCreate,
            Permissions.RedemptionsConfirm,
        ],
        [Marketing] = [Permissions.RewardsManage],
        [Analyst] = [Permissions.AnalyticsView],
    };
}
