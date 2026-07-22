using Itaris.SharedKernel;

namespace Itaris.Modules.Rewards.Features.Redeem;

/// <summary>doc 05 D10 — cashier-initiated one-step redemption: create the intent and confirm it immediately.</summary>
public sealed class OneStepRedemptionHandler(RedemptionIntentHandler intent, ConfirmRedemptionHandler confirm)
{
    public async Task<Result<ConfirmResponse>> HandleAsync(
        Guid merchantId, Guid staffId, Guid customerId, Guid rewardId, CancellationToken cancellationToken)
    {
        var intentResult = await intent.HandleAsync(merchantId, customerId, rewardId, staffId, cancellationToken);
        if (!intentResult.IsSuccess)
        {
            return Result<ConfirmResponse>.Failure(
                intentResult.ErrorCode!, intentResult.ErrorMessage!, intentResult.ErrorDetails);
        }

        return await confirm.HandleAsync(merchantId, staffId, intentResult.Value.Code, cancellationToken);
    }
}
