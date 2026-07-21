using Itaris.Modules.Loyalty.Domain;

namespace Itaris.Modules.Loyalty.Features.Memberships;

internal static class MembershipMapper
{
    /// <summary>Maps a membership to its DTO. Stamp-card total comes from the program's current rule config.</summary>
    public static MembershipDto ToDto(CustomerMembership m, LoyaltyProgram program, RuleConfig? config)
    {
        StampCardDto? stampCard = program.Type == ProgramTypes.Stamps
            ? new StampCardDto(m.StampsFilled, config?.CardSize ?? 0, m.StampCardCycle)
            : null;

        return new MembershipDto(
            m.Id, m.MerchantId, m.ProgramId, program.Type,
            m.PointsBalance, stampCard, m.JoinedAt, m.JoinSource);
    }
}
