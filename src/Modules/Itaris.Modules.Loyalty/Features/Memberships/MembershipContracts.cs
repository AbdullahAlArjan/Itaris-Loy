namespace Itaris.Modules.Loyalty.Features.Memberships;

/// <summary>doc 05 C3 body: join a merchant's active program.</summary>
public sealed record JoinProgramRequest(Guid MerchantId);

public sealed record StampCardDto(int Filled, int Total, int Cycle);

/// <summary>
/// Membership summary (doc 05 mock §11). nextReward is null until the Rewards module lands (Phase 5).
/// </summary>
public sealed record MembershipDto(
    Guid MembershipId,
    Guid MerchantId,
    Guid ProgramId,
    string ProgramType,
    long PointsBalance,
    StampCardDto? StampCard,
    DateTimeOffset JoinedAt,
    string JoinSource);

public sealed record MembershipListResponse(IReadOnlyList<MembershipDto> Items);
