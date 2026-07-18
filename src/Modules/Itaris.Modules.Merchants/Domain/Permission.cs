using Itaris.SharedKernel;

namespace Itaris.Modules.Merchants.Domain;

/// <summary>merchants.permissions — permission catalog (doc 04 Part 8). Frozen fragments: code, description.</summary>
public sealed class Permission : Entity
{
    public required string Code { get; set; }
    public required string Description { get; set; }
}
