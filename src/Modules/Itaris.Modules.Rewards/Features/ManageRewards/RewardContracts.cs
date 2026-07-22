namespace Itaris.Modules.Rewards.Features.ManageRewards;

// doc 05 F1 — create/update a reward.
public sealed record CreateRewardRequest(
    string NameAr, string NameEn, string? DescriptionAr, string? DescriptionEn,
    string CostType, long? PointsCost, long? StockRemaining, int? PerCustomerLimit);

public sealed record UpdateRewardRequest(
    string? NameAr, string? NameEn, string? DescriptionAr, string? DescriptionEn,
    long? PointsCost, long? StockRemaining, int? PerCustomerLimit);

public sealed record RewardDto(
    Guid Id, string NameAr, string NameEn, string CostType, long? PointsCost,
    long? StockRemaining, string Status);

public sealed record RewardListResponse(IReadOnlyList<RewardDto> Items);

// doc 05 B7 / F3 — a reward with its eligibility for a given customer.
public sealed record EligibleRewardDto(
    Guid RewardId, string NameAr, string NameEn, string CostType, long? PointsCost,
    bool Eligible, long? MissingPoints, bool InStock);

public sealed record EligibilityResponse(IReadOnlyList<EligibleRewardDto> Items);
