using Itaris.Modules.Loyalty.Domain;
using Itaris.Modules.Loyalty.Persistence;
using Itaris.SharedKernel;

namespace Itaris.Modules.Loyalty.Features.ManagePrograms;

/// <summary>
/// doc 05 E1 — owner/admin creates a loyalty program in draft. Type is points or stamps
/// (doc 01: "do not ship points without stamps"). Rules are set separately (E2) before activation.
/// Errors: VALIDATION_ERROR.
/// </summary>
public sealed class CreateProgramHandler(LoyaltyDbContext db)
{
    public async Task<Result<ProgramResponse>> HandleAsync(
        Guid merchantId, CreateProgramRequest request, CancellationToken cancellationToken)
    {
        if (request.Type is not (ProgramTypes.Points or ProgramTypes.Stamps))
        {
            return Result<ProgramResponse>.Failure(
                ErrorCodes.ValidationError, "Program type must be 'points' or 'stamps'.");
        }

        var program = new LoyaltyProgram
        {
            MerchantId = merchantId,
            Type = request.Type,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            Status = ProgramStatuses.Draft,
        };
        db.Programs.Add(program);
        await db.SaveChangesAsync(cancellationToken);

        return Result<ProgramResponse>.Success(
            new ProgramResponse(program.Id, program.Type, program.Status, RuleVersion: null));
    }
}
