namespace Itaris.Modules.Merchants.PublicApi;

/// <summary>
/// What the POS/transaction flows need to know about a merchant. Limits parse from the merchant
/// settings jsonb (doc 04 "settings jsonb (refund limits, …)").
/// PLACEHOLDER(doc04-Part8-clipped): exact settings key names are clipped in the PDF — using
/// "maxTransactionAmountMinor" and "refundApprovalThresholdMinor" until reconciled.
/// </summary>
public sealed record MerchantPosContext(
    Guid MerchantId,
    string Status,
    bool BranchValid,
    long? MaxTransactionAmountMinor,
    long? RefundApprovalThresholdMinor);

public interface IMerchantGateway
{
    /// <summary>Null when the merchant does not exist.</summary>
    Task<MerchantPosContext?> GetPosContextAsync(Guid merchantId, Guid branchId, CancellationToken cancellationToken);

    /// <summary>Per-staff refund ceiling override (fils); null = no override.</summary>
    Task<long?> GetStaffRefundLimitAsync(Guid staffMemberId, CancellationToken cancellationToken);
}
