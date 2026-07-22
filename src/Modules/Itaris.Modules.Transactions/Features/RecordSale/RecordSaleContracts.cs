namespace Itaris.Modules.Transactions.Features.RecordSale;

/// <summary>
/// doc 05 D4 / §9.8 body. Either customerId (resolved from QR/phone) identifies the customer.
/// duplicateOverride confirms a suspected duplicate should still be recorded.
/// </summary>
public sealed record RecordSaleRequest(
    Guid CustomerId,
    Guid BranchId,
    long AmountMinor,
    string Currency,
    DateTimeOffset? OccurredAt,
    string? Note,
    bool DuplicateOverride = false);

public sealed record LoyaltyResult(
    string Type,
    int StampsEarned,
    StampCardResult? StampCard,
    long PointsEarned,
    long NewBalance);

public sealed record StampCardResult(int Filled, int Total, bool Completed, int Cycle);

public sealed record SaleCustomerResult(string? FirstName, bool IsNewMember);

/// <summary>doc 05 §9.8 "Record sale" 201 response.</summary>
public sealed record RecordSaleResponse(
    Guid TransactionId,
    string Status,
    long AmountMinor,
    LoyaltyResult Loyalty,
    SaleCustomerResult Customer,
    DateTimeOffset RecordedAt);
