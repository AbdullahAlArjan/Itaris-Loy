namespace Itaris.Modules.Customers.PublicApi;

public sealed record CustomerSummary(Guid CustomerId, string? FirstName, string PhoneNumber, bool IsShadow);

/// <summary>Read-only customer resolution for POS/transaction flows (doc 04: Transactions depends on Customers).</summary>
public interface ICustomerDirectory
{
    Task<CustomerSummary?> GetSummaryAsync(Guid customerId, CancellationToken cancellationToken);
}
