using Itaris.Modules.Customers.Persistence;
using Itaris.Modules.Customers.PublicApi;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Customers.Features.PosCustomers;

/// <summary>Implements <see cref="ICustomerDirectory"/> over customer_profiles (read-only).</summary>
public sealed class CustomerDirectory(CustomersDbContext db) : ICustomerDirectory
{
    public async Task<CustomerSummary?> GetSummaryAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var profile = await db.Profiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == customerId, cancellationToken);
        return profile is null
            ? null
            : new CustomerSummary(profile.UserId, profile.FirstName, profile.PhoneNumber, profile.IsShadow);
    }
}
