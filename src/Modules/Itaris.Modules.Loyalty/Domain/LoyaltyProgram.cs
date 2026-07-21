using Itaris.SharedKernel;

namespace Itaris.Modules.Loyalty.Domain;

/// <summary>
/// loyalty.loyalty_programs — one program per merchant (doc 04 Part 8). Frozen fragments:
/// merchant_id, type (points/stamps), name_ar/en, status (draft/active/paused), current_rule.
/// doc 06 freeze: exactly one ACTIVE program per merchant.
/// </summary>
public sealed class LoyaltyProgram : Entity
{
    public Guid MerchantId { get; set; }

    /// <summary>points | stamps (doc 04).</summary>
    public required string Type { get; set; }

    public required string NameAr { get; set; }
    public required string NameEn { get; set; }

    public string Status { get; set; } = ProgramStatuses.Draft;

    /// <summary>Points to the current (latest active) rule version; null until the first rule is set.</summary>
    public Guid? CurrentRuleId { get; set; }
}

public static class ProgramTypes
{
    public const string Points = "points";
    public const string Stamps = "stamps";
}

public static class ProgramStatuses
{
    public const string Draft = "draft";
    public const string Active = "active";
    public const string Paused = "paused";
}
