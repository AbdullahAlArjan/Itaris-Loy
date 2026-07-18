namespace Itaris.Infrastructure.Auth;

/// <summary>
/// Everything that goes into an access token. Customer tokens set only UserId + Audience;
/// staff/owner tokens add merchant/branch/permission claims (doc 05 A7).
/// </summary>
public sealed record TokenRequest(
    Guid UserId,
    string Audience,
    Guid? MerchantId = null,
    Guid? StaffId = null,
    string? Role = null,
    IReadOnlyList<Guid>? BranchIds = null,
    IReadOnlyList<string>? Permissions = null);

public sealed record AccessToken(string Token, int ExpiresInSeconds);
