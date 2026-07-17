namespace Itaris.SharedKernel;

/// <summary>
/// Marker for commands that must be idempotent (doc 05 endpoints tagged [IDEM]).
/// The idempotency pipeline keys replay detection on the Idempotency-Key header.
/// </summary>
public interface IIdempotent;

/// <summary>Testable clock; inject instead of using DateTimeOffset.UtcNow directly.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>
/// The authenticated principal for the current request. One of the three doc 05
/// audiences: customer, staff (incl. owner), admin. Anonymous endpoints see null Id.
/// </summary>
public interface ICurrentUser
{
    Guid? Id { get; }
    string? Audience { get; }
    Guid? MerchantId { get; }
    IReadOnlyList<Guid> BranchIds { get; }
    IReadOnlyList<string> Permissions { get; }
}
