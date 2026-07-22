using Itaris.Modules.Customers.Domain;
using Itaris.Modules.Customers.Persistence;
using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Customers.Features.Profile;

/// <summary>
/// doc 05 B1/B2 — customer self profile. GetOrCreate claims a shadow profile on first authenticated
/// access (is_shadow → false) so a phone-only customer's counter history becomes "theirs" once they
/// register. Errors: VALIDATION_ERROR.
/// </summary>
public sealed class ProfileHandler(CustomersDbContext db, IClock clock)
{
    public async Task<CustomerProfileDto> GetOrCreateAsync(
        Guid userId, string phoneNumber, CancellationToken cancellationToken)
    {
        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (profile is null)
        {
            profile = new CustomerProfile { UserId = userId, PhoneNumber = phoneNumber, IsShadow = false };
            db.Profiles.Add(profile);
            await db.SaveChangesAsync(cancellationToken);
        }
        else if (profile.IsShadow)
        {
            // First real login for a previously cashier-enrolled phone → claim it.
            profile.IsShadow = false;
            profile.ClaimedAt = clock.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return ToDto(profile);
    }

    public async Task<Result<CustomerProfileDto>> UpdateAsync(
        Guid userId, string phoneNumber, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        if (request.FirstName is { Length: > 50 } || request.FirstName is "")
        {
            return Result<CustomerProfileDto>.Failure(ErrorCodes.ValidationError, "First name must be 1–50 characters.");
        }

        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile is null)
        {
            profile = new CustomerProfile { UserId = userId, PhoneNumber = phoneNumber, IsShadow = false };
            db.Profiles.Add(profile);
        }

        if (request.FirstName is not null) profile.FirstName = request.FirstName;
        if (request.BirthDate is not null) profile.BirthDate = request.BirthDate;
        if (request.Gender is not null) profile.Gender = request.Gender;
        if (request.PreferredLanguage is not null) profile.PreferredLanguage = request.PreferredLanguage;

        await db.SaveChangesAsync(cancellationToken);
        return Result<CustomerProfileDto>.Success(ToDto(profile));
    }

    private static CustomerProfileDto ToDto(CustomerProfile p) => new(
        p.Id, p.FirstName, p.PhoneNumber, p.BirthDate, p.Gender, p.PreferredLanguage, p.CreatedAt);
}
