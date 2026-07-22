using Itaris.Modules.Rewards.Domain;
using Itaris.Modules.Rewards.Persistence;
using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Rewards.Features.ManageRewards;

/// <summary>doc 05 F1 — reward CRUD + activation, merchant-scoped, gated on rewards.manage. Errors: VALIDATION_ERROR, NOT_FOUND.</summary>
public sealed class ManageRewardsHandler(RewardsDbContext db)
{
    public async Task<Result<RewardDto>> CreateAsync(
        Guid merchantId, CreateRewardRequest request, CancellationToken cancellationToken)
    {
        if (request.CostType is not (RewardCostTypes.Points or RewardCostTypes.StampCompletion))
        {
            return Result<RewardDto>.Failure(ErrorCodes.ValidationError, "costType must be 'points' or 'stamp_completion'.");
        }

        if (request.CostType == RewardCostTypes.Points && request.PointsCost is not > 0)
        {
            return Result<RewardDto>.Failure(ErrorCodes.ValidationError, "Points rewards need a positive pointsCost.");
        }

        var reward = new Reward
        {
            MerchantId = merchantId,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            DescriptionAr = request.DescriptionAr,
            DescriptionEn = request.DescriptionEn,
            CostType = request.CostType,
            PointsCost = request.CostType == RewardCostTypes.Points ? request.PointsCost : null,
            StockRemaining = request.StockRemaining,
            PerCustomerLimit = request.PerCustomerLimit,
            Status = RewardStatuses.Draft,
        };
        db.Rewards.Add(reward);
        await db.SaveChangesAsync(cancellationToken);
        return Result<RewardDto>.Success(ToDto(reward));
    }

    public async Task<Result<RewardDto>> UpdateAsync(
        Guid merchantId, Guid rewardId, UpdateRewardRequest request, CancellationToken cancellationToken)
    {
        var reward = await Find(merchantId, rewardId, cancellationToken);
        if (reward is null)
        {
            return NotFound();
        }

        if (request.NameAr is not null) reward.NameAr = request.NameAr;
        if (request.NameEn is not null) reward.NameEn = request.NameEn;
        if (request.DescriptionAr is not null) reward.DescriptionAr = request.DescriptionAr;
        if (request.DescriptionEn is not null) reward.DescriptionEn = request.DescriptionEn;
        if (request.PointsCost is not null && reward.CostType == RewardCostTypes.Points) reward.PointsCost = request.PointsCost;
        if (request.StockRemaining is not null) reward.StockRemaining = request.StockRemaining;
        if (request.PerCustomerLimit is not null) reward.PerCustomerLimit = request.PerCustomerLimit;

        await db.SaveChangesAsync(cancellationToken);
        return Result<RewardDto>.Success(ToDto(reward));
    }

    public Task<Result<RewardDto>> ActivateAsync(Guid merchantId, Guid rewardId, CancellationToken ct) =>
        SetStatusAsync(merchantId, rewardId, RewardStatuses.Active, ct);

    public Task<Result<RewardDto>> DeactivateAsync(Guid merchantId, Guid rewardId, CancellationToken ct) =>
        SetStatusAsync(merchantId, rewardId, RewardStatuses.Inactive, ct);

    public async Task<RewardListResponse> ListAsync(Guid merchantId, CancellationToken cancellationToken)
    {
        var items = await db.Rewards.AsNoTracking()
            .Where(r => r.MerchantId == merchantId)
            .OrderByDescending(r => r.Id)
            .Select(r => ToDto(r))
            .ToListAsync(cancellationToken);
        return new RewardListResponse(items);
    }

    private async Task<Result<RewardDto>> SetStatusAsync(Guid merchantId, Guid rewardId, string status, CancellationToken ct)
    {
        var reward = await Find(merchantId, rewardId, ct);
        if (reward is null)
        {
            return NotFound();
        }

        reward.Status = status;
        await db.SaveChangesAsync(ct);
        return Result<RewardDto>.Success(ToDto(reward));
    }

    private Task<Reward?> Find(Guid merchantId, Guid rewardId, CancellationToken ct) =>
        db.Rewards.FirstOrDefaultAsync(r => r.Id == rewardId && r.MerchantId == merchantId, ct);

    private static Result<RewardDto> NotFound() => Result<RewardDto>.Failure(ErrorCodes.NotFound, "Reward not found.");

    private static RewardDto ToDto(Reward r) =>
        new(r.Id, r.NameAr, r.NameEn, r.CostType, r.PointsCost, r.StockRemaining, r.Status);
}
