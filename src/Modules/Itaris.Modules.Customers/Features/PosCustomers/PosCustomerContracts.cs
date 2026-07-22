namespace Itaris.Modules.Customers.Features.PosCustomers;

/// <summary>doc 05 D3 body: { phoneNumber } → shadow profile (+ membership in Phase 4).</summary>
public sealed record EnrollRequest(string PhoneNumber);

public sealed record EnrollResponse(Guid CustomerId, Guid ProfileId, bool IsNewCustomer);

/// <summary>doc 05 D2 — masked lookup result.</summary>
public sealed record CustomerMatchDto(Guid CustomerId, string? FirstName, string PhoneMasked);

public sealed record LookupResponse(IReadOnlyList<CustomerMatchDto> Matches);
