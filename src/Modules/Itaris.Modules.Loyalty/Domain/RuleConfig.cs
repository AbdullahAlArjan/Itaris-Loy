namespace Itaris.Modules.Loyalty.Domain;

/// <summary>
/// Typed view of loyalty_rules.config (doc 04 fragments: rounding, min_amount, welcome_bonus,
/// card_size, stamps_per_visit, max_stamps, expiry_months). Serialized as jsonb on the rule row.
/// Fields are exactly those doc 04 lists — no extra rule knobs (doc 06 Phase 3 risk: no scope creep).
/// </summary>
public sealed record RuleConfig
{
    // ---- Points programs ----
    // PLACEHOLDER(doc04-Part8-clipped): the points earn-rate field name is clipped in the PDF.
    // doc 01 specifies "points-per-JOD with rounding rule", so we model it as points per 1 JOD.
    public decimal PointsPerJod { get; init; }

    /// <summary>How fractional earned points are resolved (doc 04 "rounding").</summary>
    public RoundingMode Rounding { get; init; } = RoundingMode.Floor;

    /// <summary>Minimum purchase (fils) to earn anything (doc 04 "min_amount"). 0 = no minimum.</summary>
    public long MinAmountMinor { get; init; }

    /// <summary>Points (points programs) or stamps (stamp programs) granted on join (doc 04 "welcome_bonus").</summary>
    public int WelcomeBonus { get; init; }

    // ---- Stamp programs ----
    /// <summary>Stamps needed to complete one card (doc 04 "card_size"). e.g. buy 9 get 1 free → 9.</summary>
    public int CardSize { get; init; }

    /// <summary>Stamps granted per qualifying visit (doc 04 "stamps_per_visit"). Usually 1.</summary>
    public int StampsPerVisit { get; init; } = 1;

    /// <summary>Cap on stamps from a single visit (doc 04 "max_stamps"). Usually 1.</summary>
    public int MaxStampsPerVisit { get; init; } = 1;

    // ---- Shared ----
    /// <summary>Points expiry in months (doc 04 "expiry_months"). Schema-supported; enforcement is v1.1 (doc 01).</summary>
    public int? ExpiryMonths { get; init; }
}

public enum RoundingMode
{
    Floor,
    Nearest,
    Ceiling,
}
