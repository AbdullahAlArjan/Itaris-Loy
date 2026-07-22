namespace Itaris.Modules.Rewards.Features.Redeem;

/// <summary>doc 05 B9 body: { rewardId }.</summary>
public sealed record CreateIntentRequest(Guid RewardId);

/// <summary>doc 05 B9 §9.8 response.</summary>
public sealed record IntentResponse(
    Guid RedemptionId, string Code, string Status, DateTimeOffset ExpiresAt,
    long PointsHeld, bool StampCardConsumed);

/// <summary>doc 05 D9 body: { redemptionCode, branchId }.</summary>
public sealed record ConfirmRequest(string RedemptionCode, Guid BranchId);

public sealed record ConfirmResponse(Guid RedemptionId, string Status, DateTimeOffset ConfirmedAt);

/// <summary>doc 05 D10 body — cashier-initiated one-step: { customerId, rewardId, branchId }.</summary>
public sealed record OneStepRequest(Guid CustomerId, Guid RewardId, Guid BranchId);

public sealed record RedemptionDto(
    Guid Id, Guid RewardId, string Status, string Code, DateTimeOffset ExpiresAt, DateTimeOffset? ConfirmedAt);

public sealed record RedemptionListResponse(IReadOnlyList<RedemptionDto> Items);
