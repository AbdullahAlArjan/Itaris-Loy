using Itaris.SharedKernel;

namespace Itaris.Modules.Merchants.Domain;

/// <summary>
/// merchants.branches — physical locations (doc 04 Part 8). Frozen fragments: merchant_id,
/// name_en, area, geo point, phone. doc 01: "CRUD, but simple: name, address, geo point, active flag".
/// </summary>
public sealed class Branch : Entity
{
    public Guid MerchantId { get; set; }
    public required string NameAr { get; set; }
    public required string NameEn { get; set; }
    public string? AreaAr { get; set; }
    public string? AreaEn { get; set; }
    public string? AddressAr { get; set; }
    public string? AddressEn { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
}
