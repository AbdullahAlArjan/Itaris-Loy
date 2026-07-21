using Itaris.Modules.Loyalty.Domain;
using Itaris.Modules.Loyalty.Persistence;
using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Loyalty.Features.Memberships;

/// <summary>
/// doc 05 C3 — a customer joins a merchant's active program. Idempotent: re-joining returns the
/// existing membership. The welcome bonus (rule config) is applied to the projected balance on join.
/// Membership is per-merchant, not per-branch (doc 06 freeze). Errors: PROGRAM_INACTIVE.
/// </summary>
public sealed class JoinProgramHandler(LoyaltyDbContext db, IClock clock)
{
    public async Task<Result<MembershipDto>> HandleAsync(
        Guid customerId, JoinProgramRequest request, CancellationToken cancellationToken)
    {
        var program = await db.Programs.FirstOrDefaultAsync(
            p => p.MerchantId == request.MerchantId && p.Status == ProgramStatuses.Active, cancellationToken);
        if (program is null)
        {
            return Result<MembershipDto>.Failure(
                ErrorCodes.ProgramInactive, "This merchant has no active loyalty program.");
        }

        var config = await LoadConfig(program, cancellationToken);

        var existing = await db.Memberships.FirstOrDefaultAsync(
            m => m.CustomerId == customerId && m.MerchantId == request.MerchantId, cancellationToken);
        if (existing is not null)
        {
            return Result<MembershipDto>.Success(MembershipMapper.ToDto(existing, program, config));
        }

        var membership = new CustomerMembership
        {
            CustomerId = customerId,
            MerchantId = request.MerchantId,
            ProgramId = program.Id,
            JoinedAt = clock.UtcNow,
            JoinSource = MembershipJoinSources.App,
        };

        // Welcome bonus (doc 04 "welcome_bonus") into the projected balance.
        var welcome = config?.WelcomeBonus ?? 0;
        if (welcome > 0)
        {
            if (program.Type == ProgramTypes.Points)
            {
                membership.PointsBalance = welcome;
            }
            else
            {
                membership.StampsFilled = config!.CardSize > 0 ? Math.Min(welcome, config.CardSize) : welcome;
            }
        }

        db.Memberships.Add(membership);
        await db.SaveChangesAsync(cancellationToken);

        return Result<MembershipDto>.Success(MembershipMapper.ToDto(membership, program, config));
    }

    private async Task<RuleConfig?> LoadConfig(LoyaltyProgram program, CancellationToken ct)
    {
        if (program.CurrentRuleId is not { } ruleId)
        {
            return null;
        }

        var rule = await db.Rules.FirstOrDefaultAsync(r => r.Id == ruleId, ct);
        return rule is null ? null : LoyaltyJson.Deserialize(rule.ConfigJson);
    }
}
