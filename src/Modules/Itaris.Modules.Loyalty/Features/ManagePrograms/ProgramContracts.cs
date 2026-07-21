using Itaris.Modules.Loyalty.Domain;

namespace Itaris.Modules.Loyalty.Features.ManagePrograms;

// doc 05 E1 — create a program (draft).
public sealed record CreateProgramRequest(string Type, string NameAr, string NameEn);

public sealed record ProgramResponse(Guid ProgramId, string Type, string Status, int? RuleVersion);

// doc 05 E2 — set/replace the program's rules, creating a new version.
// Fields mirror RuleConfig exactly (doc 06: no extra rule knobs).
public sealed record UpdateRulesRequest(
    decimal PointsPerJod,
    RoundingMode Rounding,
    long MinAmountMinor,
    int WelcomeBonus,
    int CardSize,
    int StampsPerVisit,
    int MaxStampsPerVisit,
    int? ExpiryMonths)
{
    public RuleConfig ToConfig() => new()
    {
        PointsPerJod = PointsPerJod,
        Rounding = Rounding,
        MinAmountMinor = MinAmountMinor,
        WelcomeBonus = WelcomeBonus,
        CardSize = CardSize,
        StampsPerVisit = StampsPerVisit,
        MaxStampsPerVisit = MaxStampsPerVisit,
        ExpiryMonths = ExpiryMonths,
    };
}

public sealed record UpdateRulesResponse(Guid ProgramId, int RuleVersion);
