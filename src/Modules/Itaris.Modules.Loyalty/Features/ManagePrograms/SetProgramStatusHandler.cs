using Itaris.Modules.Loyalty.Domain;
using Itaris.Modules.Loyalty.Persistence;
using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Loyalty.Features.ManagePrograms;

/// <summary>
/// doc 05 E3 — activate or pause a program. A merchant may have exactly one ACTIVE program at a
/// time (doc 06 freeze): activating while another is active → PROGRAM_LIMIT (the DB partial unique
/// index is the safety net). A program must have rules before it can go active.
/// Errors: NOT_FOUND, PROGRAM_LIMIT, VALIDATION_ERROR.
/// </summary>
public sealed class SetProgramStatusHandler(LoyaltyDbContext db)
{
    public async Task<Result<ProgramResponse>> ActivateAsync(
        Guid merchantId, Guid programId, CancellationToken cancellationToken)
    {
        var program = await Find(merchantId, programId, cancellationToken);
        if (program is null)
        {
            return NotFound();
        }

        if (program.CurrentRuleId is null)
        {
            return Result<ProgramResponse>.Failure(
                ErrorCodes.ValidationError, "Set the program's rules before activating it.");
        }

        if (program.Status != ProgramStatuses.Active)
        {
            var anotherActive = await db.Programs.AnyAsync(
                p => p.MerchantId == merchantId && p.Status == ProgramStatuses.Active && p.Id != programId,
                cancellationToken);
            if (anotherActive)
            {
                return Result<ProgramResponse>.Failure(
                    ErrorCodes.ProgramLimit, "This merchant already has an active program.");
            }

            program.Status = ProgramStatuses.Active;
            await db.SaveChangesAsync(cancellationToken);
        }

        return Ok(program);
    }

    public async Task<Result<ProgramResponse>> PauseAsync(
        Guid merchantId, Guid programId, CancellationToken cancellationToken)
    {
        var program = await Find(merchantId, programId, cancellationToken);
        if (program is null)
        {
            return NotFound();
        }

        if (program.Status == ProgramStatuses.Active)
        {
            program.Status = ProgramStatuses.Paused;
            await db.SaveChangesAsync(cancellationToken);
        }

        return Ok(program);
    }

    private Task<LoyaltyProgram?> Find(Guid merchantId, Guid programId, CancellationToken ct) =>
        db.Programs.FirstOrDefaultAsync(p => p.Id == programId && p.MerchantId == merchantId, ct);

    private static Result<ProgramResponse> NotFound() =>
        Result<ProgramResponse>.Failure(ErrorCodes.NotFound, "Program not found.");

    private static Result<ProgramResponse> Ok(LoyaltyProgram p) =>
        Result<ProgramResponse>.Success(new ProgramResponse(p.Id, p.Type, p.Status, RuleVersion: null));
}
