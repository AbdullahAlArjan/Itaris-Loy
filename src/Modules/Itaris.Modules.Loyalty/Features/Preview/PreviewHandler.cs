using Itaris.Modules.Loyalty.Domain;
using Itaris.Modules.Loyalty.Persistence;
using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Loyalty.Features.Preview;

/// <summary>
/// doc 05 E4 — previews what a purchase of amountMinor would earn under a program's current rule.
/// Pure calculation over the rule config (doc 04 CalculatePointsPreview). Powers the cashier's live
/// preview and the dashboard rule preview. Errors: NOT_FOUND, VALIDATION_ERROR.
/// </summary>
public sealed class PreviewHandler(LoyaltyDbContext db)
{
    public async Task<Result<PreviewResponse>> HandleAsync(
        Guid merchantId, PreviewRequest request, CancellationToken cancellationToken)
    {
        if (request.AmountMinor < 0)
        {
            return Result<PreviewResponse>.Failure(ErrorCodes.ValidationError, "Amount cannot be negative.");
        }

        var program = await db.Programs
            .FirstOrDefaultAsync(p => p.Id == request.ProgramId && p.MerchantId == merchantId, cancellationToken);
        if (program is null || program.CurrentRuleId is null)
        {
            return Result<PreviewResponse>.Failure(ErrorCodes.NotFound, "Program or its rules not found.");
        }

        var rule = await db.Rules.FirstAsync(r => r.Id == program.CurrentRuleId, cancellationToken);
        var config = LoyaltyJson.Deserialize(rule.ConfigJson);

        var result = LoyaltyCalculator.Calculate(program.Type, config, request.AmountMinor);
        return Result<PreviewResponse>.Success(new PreviewResponse(result.PointsEarned, result.StampsEarned));
    }
}
