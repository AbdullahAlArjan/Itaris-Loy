using System.Text.Json;
using Itaris.Modules.Merchants.Persistence;
using Itaris.Modules.Merchants.PublicApi;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Merchants.Features.Access;

/// <summary>Implements <see cref="IMerchantGateway"/> over the merchants schema (read-only).</summary>
public sealed class MerchantGateway(MerchantsDbContext db) : IMerchantGateway
{
    public async Task<MerchantPosContext?> GetPosContextAsync(
        Guid merchantId, Guid branchId, CancellationToken cancellationToken)
    {
        var merchant = await db.Merchants.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == merchantId, cancellationToken);
        if (merchant is null)
        {
            return null;
        }

        var branchValid = await db.Branches.AsNoTracking()
            .AnyAsync(b => b.Id == branchId && b.MerchantId == merchantId && b.IsActive, cancellationToken);

        long? maxTx = null;
        long? refundThreshold = null;
        try
        {
            using var settings = JsonDocument.Parse(merchant.SettingsJson);
            if (settings.RootElement.TryGetProperty("maxTransactionAmountMinor", out var maxEl) &&
                maxEl.TryGetInt64(out var max))
            {
                maxTx = max;
            }

            if (settings.RootElement.TryGetProperty("refundApprovalThresholdMinor", out var thEl) &&
                thEl.TryGetInt64(out var th))
            {
                refundThreshold = th;
            }
        }
        catch (JsonException)
        {
            // Malformed settings → treat as no limits configured.
        }

        return new MerchantPosContext(merchant.Id, merchant.Status, branchValid, maxTx, refundThreshold);
    }

    public Task<long?> GetStaffRefundLimitAsync(Guid staffMemberId, CancellationToken cancellationToken) =>
        db.StaffMembers.AsNoTracking()
            .Where(s => s.Id == staffMemberId)
            .Select(s => s.RefundLimitMinor)
            .FirstOrDefaultAsync(cancellationToken);
}
