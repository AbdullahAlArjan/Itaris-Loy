using Itaris.SharedKernel;

namespace Itaris.Modules.Merchants.Domain;

/// <summary>
/// merchants.merchants — tenant root (doc 04 Part 8). Frozen fragments: name_ar, category,
/// en (name_en), logo_url, status (pending/active/paused), settings jsonb (refund limits, auto…).
/// Localized fields are stored as separate _ar/_en columns and surfaced as { ar, en } per doc 05.
/// </summary>
public sealed class Merchant : Entity
{
    /// <summary>Short human-enterable code used at staff PIN login (doc 05 A7 merchantCode).</summary>
    public required string Code { get; set; }

    public required string NameAr { get; set; }
    public required string NameEn { get; set; }
    public required string Category { get; set; }
    public string? DescriptionAr { get; set; }
    public string? DescriptionEn { get; set; }
    public string? LogoUrl { get; set; }
    public string Status { get; set; } = MerchantStatuses.Pending;

    /// <summary>Refund limits, auto-approve flags, etc. (doc 04 "settings jsonb").</summary>
    public string SettingsJson { get; set; } = "{}";
}

public static class MerchantStatuses
{
    public const string Pending = "pending";
    public const string Active = "active";
    public const string Paused = "paused";
}
