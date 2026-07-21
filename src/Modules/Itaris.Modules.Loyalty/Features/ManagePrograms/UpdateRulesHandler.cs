using Itaris.Modules.Loyalty.Domain;
using Itaris.Modules.Loyalty.Persistence;
using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Loyalty.Features.ManagePrograms;

/// <summary>
/// doc 05 E2 — sets the program's rules by creating a NEW immutable rule version and pointing the
/// program at it. Past transactions keep their version's terms. Errors: NOT_FOUND, VALIDATION_ERROR.
/// </summary>
public sealed class UpdateRulesHandler(LoyaltyDbContext db, IClock clock)
{
    public async Task<Result<UpdateRulesResponse>> HandleAsync(
        Guid merchantId, Guid programId, UpdateRulesRequest request, CancellationToken cancellationToken)
    {
        var program = await db.Programs
            .FirstOrDefaultAsync(p => p.Id == programId && p.MerchantId == merchantId, cancellationToken);
        if (program is null)
        {
            return Result<UpdateRulesResponse>.Failure(ErrorCodes.NotFound, "Program not found.");
        }

        if (!IsValid(program.Type, request, out var error))
        {
            return Result<UpdateRulesResponse>.Failure(ErrorCodes.ValidationError, error);
        }

        var nextVersion = await db.Rules
            .Where(r => r.ProgramId == programId)
            .Select(r => (int?)r.Version)
            .MaxAsync(cancellationToken) ?? 0;
        nextVersion++;

        var rule = new LoyaltyRule
        {
            ProgramId = programId,
            Version = nextVersion,
            ConfigJson = LoyaltyJson.Serialize(request.ToConfig()),
            EffectiveFrom = clock.UtcNow,
        };
        db.Rules.Add(rule);

        program.CurrentRuleId = rule.Id;
        await db.SaveChangesAsync(cancellationToken);

        return Result<UpdateRulesResponse>.Success(new UpdateRulesResponse(programId, nextVersion));
    }

    private static bool IsValid(string programType, UpdateRulesRequest r, out string error)
    {
        if (r.MinAmountMinor < 0 || r.WelcomeBonus < 0)
        {
            error = "Amounts and bonuses cannot be negative.";
            return false;
        }

        if (programType == ProgramTypes.Points && r.PointsPerJod <= 0)
        {
            error = "Points programs need a positive pointsPerJod rate.";
            return false;
        }

        if (programType == ProgramTypes.Stamps && (r.CardSize <= 0 || r.StampsPerVisit <= 0 || r.MaxStampsPerVisit <= 0))
        {
            error = "Stamp programs need positive cardSize, stampsPerVisit, and maxStampsPerVisit.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
