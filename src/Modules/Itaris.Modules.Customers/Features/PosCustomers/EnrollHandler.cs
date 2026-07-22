using Itaris.Modules.Customers.Domain;
using Itaris.Modules.Customers.Persistence;
using Itaris.Modules.Identity.PublicApi;
using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Customers.Features.PosCustomers;

/// <summary>
/// doc 05 D3 — a cashier enrolls a phone-only customer (doc 01: phone-number fallback is first-class).
/// Idempotent: enrolling an existing phone returns the existing customer. Creates the identity user
/// (keyed by phone, so a later OTP login reuses it) and a shadow profile. Auto-join to the merchant's
/// active program is added with the Transactions flow (Phase 4).
/// </summary>
public sealed class EnrollHandler(CustomersDbContext db, IUserDirectory users)
{
    public async Task<Result<EnrollResponse>> HandleAsync(EnrollRequest request, CancellationToken cancellationToken)
    {
        var phone = request.PhoneNumber.Trim();

        var userId = await users.EnsureCustomerByPhoneAsync(phone, cancellationToken);

        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile is not null)
        {
            return Result<EnrollResponse>.Success(new EnrollResponse(userId, profile.Id, IsNewCustomer: false));
        }

        profile = new CustomerProfile { UserId = userId, PhoneNumber = phone, IsShadow = true };
        db.Profiles.Add(profile);
        await db.SaveChangesAsync(cancellationToken);

        return Result<EnrollResponse>.Success(new EnrollResponse(userId, profile.Id, IsNewCustomer: true));
    }
}
