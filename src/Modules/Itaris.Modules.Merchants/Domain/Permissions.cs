namespace Itaris.Modules.Merchants.Domain;

/// <summary>
/// Permission-string catalog (doc 01 Part 3: "model as permission strings … checked via ASP.NET
/// Core authorization policies. Never hardcode role names in business logic"). doc 04 seeds these.
/// Strings observed in doc 05 perms column (customers.identify, transactions.record,
/// refunds.create, redemptions.confirm, programs.manage, rewards.manage, analytics.view,
/// audit.view) plus doc 01 (refunds.approve, staff.manage).
/// </summary>
public static class Permissions
{
    public const string MerchantManage = "merchant.manage";
    public const string BranchesManage = "branches.manage";
    public const string StaffManage = "staff.manage";
    public const string SubscriptionManage = "subscription.manage";
    public const string ProgramsManage = "programs.manage";
    public const string RewardsManage = "rewards.manage";
    public const string CustomersIdentify = "customers.identify";
    public const string TransactionsRecord = "transactions.record";
    public const string RefundsCreate = "refunds.create";
    public const string RefundsApprove = "refunds.approve";
    public const string RedemptionsConfirm = "redemptions.confirm";
    public const string PointsAdjust = "points.adjust";
    public const string AnalyticsView = "analytics.view";
    public const string AuditView = "audit.view";

    public static readonly IReadOnlyDictionary<string, string> Catalog = new Dictionary<string, string>
    {
        [MerchantManage] = "Edit merchant profile and settings",
        [BranchesManage] = "Create and edit branches",
        [StaffManage] = "Invite, update, and remove staff",
        [SubscriptionManage] = "Manage subscription and billing",
        [ProgramsManage] = "Create and edit loyalty programs",
        [RewardsManage] = "Create and edit rewards",
        [CustomersIdentify] = "Identify customers by QR or phone",
        [TransactionsRecord] = "Record sales transactions",
        [RefundsCreate] = "Issue refunds within limit",
        [RefundsApprove] = "Approve refunds above cashier limit",
        [RedemptionsConfirm] = "Confirm reward redemptions",
        [PointsAdjust] = "Manually adjust points with reason",
        [AnalyticsView] = "View analytics and reports",
        [AuditView] = "View audit logs and fraud flags",
    };
}
