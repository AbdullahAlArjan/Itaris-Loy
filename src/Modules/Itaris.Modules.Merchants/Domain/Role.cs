using Itaris.SharedKernel;

namespace Itaris.Modules.Merchants.Domain;

/// <summary>
/// merchants.roles — role templates (doc 04 Part 8). Frozen fragments: merchant_id (null =
/// platform/system template), is_system. Seeded system roles are merchant-agnostic (MerchantId null).
/// </summary>
public sealed class Role : Entity
{
    /// <summary>Null for seeded system roles shared across all merchants.</summary>
    public Guid? MerchantId { get; set; }

    public required string Name { get; set; }
    public bool IsSystem { get; set; }
}
