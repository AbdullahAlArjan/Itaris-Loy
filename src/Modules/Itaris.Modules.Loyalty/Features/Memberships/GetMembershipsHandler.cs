using Itaris.Modules.Loyalty.Domain;
using Itaris.Modules.Loyalty.Persistence;
using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Loyalty.Features.Memberships;

/// <summary>doc 05 B4 (list) and B5 (detail) — a customer's own memberships with balance + stamp state.</summary>
public sealed class GetMembershipsHandler(LoyaltyDbContext db)
{
    public async Task<MembershipListResponse> ListAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var memberships = await db.Memberships
            .Where(m => m.CustomerId == customerId)
            .OrderByDescending(m => m.JoinedAt)
            .ToListAsync(cancellationToken);

        var items = new List<MembershipDto>(memberships.Count);
        foreach (var membership in memberships)
        {
            items.Add(await MapAsync(membership, cancellationToken));
        }

        return new MembershipListResponse(items);
    }

    public async Task<Result<MembershipDto>> DetailAsync(
        Guid customerId, Guid membershipId, CancellationToken cancellationToken)
    {
        var membership = await db.Memberships.FirstOrDefaultAsync(
            m => m.Id == membershipId && m.CustomerId == customerId, cancellationToken);
        if (membership is null)
        {
            return Result<MembershipDto>.Failure(ErrorCodes.NotFound, "Membership not found.");
        }

        return Result<MembershipDto>.Success(await MapAsync(membership, cancellationToken));
    }

    private async Task<MembershipDto> MapAsync(CustomerMembership membership, CancellationToken ct)
    {
        var program = await db.Programs.FirstAsync(p => p.Id == membership.ProgramId, ct);
        RuleConfig? config = null;
        if (program.CurrentRuleId is { } ruleId)
        {
            var rule = await db.Rules.FirstOrDefaultAsync(r => r.Id == ruleId, ct);
            config = rule is null ? null : LoyaltyJson.Deserialize(rule.ConfigJson);
        }

        return MembershipMapper.ToDto(membership, program, config);
    }
}
