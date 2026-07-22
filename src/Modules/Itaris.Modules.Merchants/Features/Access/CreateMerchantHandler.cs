using Itaris.Modules.Identity.PublicApi;
using Itaris.Modules.Merchants.Domain;
using Itaris.Modules.Merchants.Persistence;
using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Merchants.Features.Access;

/// <summary>
/// doc 05 C10 — platform admin creates a merchant and provisions its owner: an identity user
/// (email/password), an active owner staff_member linked to that user, and the owner system role
/// across all branches. Self-serve onboarding is out of MVP (doc 01), so this is admin-only.
/// </summary>
public sealed class CreateMerchantHandler(MerchantsDbContext db, IUserDirectory users)
{
    public async Task<Result<CreateMerchantResponse>> HandleAsync(
        CreateMerchantRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Owner.Email.Trim().ToLowerInvariant();

        var ownerUserId = await users.CreateOwnerAsync(normalizedEmail, request.Owner.Password, cancellationToken);

        var merchant = new Merchant
        {
            Code = await GenerateUniqueCodeAsync(request.NameEn, cancellationToken),
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            Category = request.Category,
            Status = MerchantStatuses.Active,
        };
        db.Merchants.Add(merchant);

        // Every merchant starts with one branch so the cashier can record sales immediately
        // (branch CRUD, doc 05 C6, remains for later — this is the default).
        var branch = new Branch
        {
            MerchantId = merchant.Id,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            IsActive = true,
        };
        db.Branches.Add(branch);

        var ownerStaff = new StaffMember
        {
            MerchantId = merchant.Id,
            UserId = ownerUserId,
            DisplayName = request.NameEn,
            PhoneOrEmail = normalizedEmail,
            Status = StaffStatuses.Active,
        };
        db.StaffMembers.Add(ownerStaff);

        db.StaffRoles.Add(new StaffRole
        {
            StaffMemberId = ownerStaff.Id,
            RoleId = DeterministicGuid.Create($"role:{SystemRoles.Owner}"),
            BranchId = null,
        });

        await db.SaveChangesAsync(cancellationToken);

        return Result<CreateMerchantResponse>.Success(
            new CreateMerchantResponse(merchant.Id, merchant.Code, ownerUserId, branch.Id));
    }

    private async Task<string> GenerateUniqueCodeAsync(string nameEn, CancellationToken cancellationToken)
    {
        var slug = new string(nameEn.ToUpperInvariant().Where(char.IsLetterOrDigit).Take(6).ToArray());
        if (slug.Length == 0)
        {
            slug = "MRC";
        }

        for (var attempt = 0; attempt < 20; attempt++)
        {
            var candidate = $"{slug}{Random.Shared.Next(100, 1000)}";
            if (!await db.Merchants.AnyAsync(m => m.Code == candidate, cancellationToken))
            {
                return candidate;
            }
        }

        return $"{slug}{Guid.NewGuid():N}"[..12];
    }
}
