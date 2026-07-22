using Itaris.Modules.Rewards.Persistence;
using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Rewards.Features.Redeem;

/// <summary>doc 05 B10 (poll), B8 (customer history), F2 (merchant redemptions).</summary>
public sealed class RedemptionReadsHandler(RewardsDbContext db)
{
    public async Task<Result<RedemptionDto>> PollAsync(
        Guid customerId, Guid redemptionId, CancellationToken cancellationToken)
    {
        var r = await db.Redemptions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == redemptionId && x.CustomerId == customerId, cancellationToken);
        return r is null
            ? Result<RedemptionDto>.Failure(ErrorCodes.RedemptionNotFound, "Redemption not found.")
            : Result<RedemptionDto>.Success(ToDto(r));
    }

    public async Task<RedemptionListResponse> ListForCustomerAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var items = await db.Redemptions.AsNoTracking()
            .Where(r => r.CustomerId == customerId)
            .OrderByDescending(r => r.Id)
            .Take(100)
            .Select(r => ToDto(r))
            .ToListAsync(cancellationToken);
        return new RedemptionListResponse(items);
    }

    public async Task<RedemptionListResponse> ListForMerchantAsync(
        Guid merchantId, Guid? rewardId, CancellationToken cancellationToken)
    {
        var query = db.Redemptions.AsNoTracking().Where(r => r.MerchantId == merchantId);
        if (rewardId is { } rid)
        {
            query = query.Where(r => r.RewardId == rid);
        }

        var items = await query
            .OrderByDescending(r => r.Id)
            .Take(100)
            .Select(r => ToDto(r))
            .ToListAsync(cancellationToken);
        return new RedemptionListResponse(items);
    }

    private static RedemptionDto ToDto(Domain.Redemption r) =>
        new(r.Id, r.RewardId, r.Status, r.Code, r.ExpiresAt, r.ConfirmedAt);
}
