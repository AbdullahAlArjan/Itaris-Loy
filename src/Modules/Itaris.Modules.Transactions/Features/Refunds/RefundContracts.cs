namespace Itaris.Modules.Transactions.Features.Refunds;

/// <summary>doc 05 D7 body: { type: "full" | "partial", amountMinor?, reason }.</summary>
public sealed record RefundRequest(string Type, long? AmountMinor, string? Reason);

public sealed record LoyaltyReversalResult(long PointsClawback, int StampsClawback);

/// <summary>doc 05 §9.8 partial-refund response.</summary>
public sealed record RefundResponse(
    Guid RefundId,
    string Type,
    long AmountMinor,
    LoyaltyReversalResult LoyaltyReversal,
    string TransactionStatus);
