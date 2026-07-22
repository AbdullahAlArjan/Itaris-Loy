using Itaris.Modules.Customers.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Customers.Features.PosCustomers;

/// <summary>
/// doc 05 D2 — cashier looks up customers by phone (full or trailing digits). Results are masked
/// (e.g. 079•••0001) so a cashier can identify without seeing the full number. Audited elsewhere.
/// </summary>
public sealed class LookupHandler(CustomersDbContext db)
{
    private const int MaxMatches = 10;

    public async Task<LookupResponse> HandleAsync(string phoneFragment, CancellationToken cancellationToken)
    {
        var digits = new string(phoneFragment.Where(char.IsDigit).ToArray());
        if (digits.Length < 4)
        {
            return new LookupResponse([]);
        }

        var profiles = await db.Profiles
            .Where(p => p.PhoneNumber.Contains(digits))
            .OrderBy(p => p.PhoneNumber)
            .Take(MaxMatches)
            .ToListAsync(cancellationToken);

        var matches = profiles
            .Select(p => new CustomerMatchDto(p.UserId, p.FirstName, Mask(p.PhoneNumber)))
            .ToList();

        return new LookupResponse(matches);
    }

    /// <summary>Keeps the leading 3 and trailing 4 digits, masks the middle: 079•••0001.</summary>
    internal static string Mask(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length <= 7)
        {
            return new string('•', digits.Length);
        }

        var national = digits.StartsWith("962") ? "0" + digits[3..] : digits;
        return $"{national[..3]}•••{national[^4..]}";
    }
}
