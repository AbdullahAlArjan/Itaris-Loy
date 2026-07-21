using Itaris.SharedKernel;

namespace Itaris.Modules.Loyalty.Domain;

/// <summary>
/// loyalty.loyalty_rules — versioned rule snapshots (doc 04 Part 8). Frozen fragments:
/// program_id, config jsonb, effective_from, (version). Rules are immutable once created;
/// editing rules (doc 05 E2) creates a NEW version so past transactions keep their terms.
/// </summary>
public sealed class LoyaltyRule : Entity
{
    public Guid ProgramId { get; set; }

    /// <summary>Monotonic per program, starting at 1.</summary>
    public int Version { get; set; }

    /// <summary>Serialized <see cref="RuleConfig"/>; jsonb column.</summary>
    public required string ConfigJson { get; set; }

    public DateTimeOffset EffectiveFrom { get; set; }
}
