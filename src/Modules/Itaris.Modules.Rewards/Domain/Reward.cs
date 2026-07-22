using Itaris.SharedKernel;

namespace Itaris.Modules.Rewards.Domain;

/// <summary>
/// rewards.rewards — reward catalog (doc 04 Part 8). Frozen fragments: merchant_id, name_ar/en,
/// description, image_url, cost_type (points/stamp_completion), points_cost (null = stamp),
/// stock_remaining (null = unlimited), per_customer_limit, valid_from/until, status (draft/active).
/// </summary>
public sealed class Reward : Entity
{
    public Guid MerchantId { get; set; }
    public required string NameAr { get; set; }
    public required string NameEn { get; set; }
    public string? DescriptionAr { get; set; }
    public string? DescriptionEn { get; set; }
    public string? ImageUrl { get; set; }

    /// <summary>points | stamp_completion (doc 04 cost_type).</summary>
    public required string CostType { get; set; }

    /// <summary>Points needed for a points reward; null for a stamp-completion reward.</summary>
    public long? PointsCost { get; set; }

    /// <summary>Remaining stock; null = unlimited. Decremented (held) on redemption intent.</summary>
    public long? StockRemaining { get; set; }

    /// <summary>Max redemptions per customer; null = unlimited.</summary>
    public int? PerCustomerLimit { get; set; }

    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset? ValidUntil { get; set; }

    public string Status { get; set; } = RewardStatuses.Draft;
}

public static class RewardCostTypes
{
    public const string Points = "points";
    public const string StampCompletion = "stamp_completion";
}

public static class RewardStatuses
{
    public const string Draft = "draft";
    public const string Active = "active";
    public const string Inactive = "inactive";
}
