using Itaris.Modules.Merchants.Domain;
using Itaris.Modules.Merchants.Persistence;
using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Merchants.Features.Access;

/// <summary>doc 05 C10 — PATCH /admin/merchants/{id} body { status: "active" | "paused" }.</summary>
public sealed record SetMerchantStatusRequest(string Status);

public sealed record SetMerchantStatusResponse(Guid MerchantId, string Status);

/// <summary>
/// doc 05 C10 — platform admin pauses or reactivates a merchant. Setting <c>paused</c> is how a
/// merchant that stopped paying its subscription (billing is manual in the MVP, doc 01) is taken
/// offline. This flips the status; enforcement (blocking new transactions while paused) lands with
/// the Transactions module (Phase 4). The change is audited automatically via the audit interceptor.
/// Errors: VALIDATION_ERROR, MERCHANT_NOT_FOUND.
/// </summary>
public sealed class SetMerchantStatusHandler(MerchantsDbContext db)
{
    private static readonly string[] AllowedStatuses = [MerchantStatuses.Active, MerchantStatuses.Paused];

    public async Task<Result<SetMerchantStatusResponse>> HandleAsync(
        Guid merchantId, SetMerchantStatusRequest request, CancellationToken cancellationToken)
    {
        if (!AllowedStatuses.Contains(request.Status))
        {
            return Result<SetMerchantStatusResponse>.Failure(
                ErrorCodes.ValidationError, $"Status must be one of: {string.Join(", ", AllowedStatuses)}.");
        }

        var merchant = await db.Merchants.FirstOrDefaultAsync(m => m.Id == merchantId, cancellationToken);
        if (merchant is null)
        {
            return Result<SetMerchantStatusResponse>.Failure(ErrorCodes.MerchantNotFound, "Merchant not found.");
        }

        merchant.Status = request.Status;
        await db.SaveChangesAsync(cancellationToken);

        return Result<SetMerchantStatusResponse>.Success(
            new SetMerchantStatusResponse(merchant.Id, merchant.Status));
    }
}
