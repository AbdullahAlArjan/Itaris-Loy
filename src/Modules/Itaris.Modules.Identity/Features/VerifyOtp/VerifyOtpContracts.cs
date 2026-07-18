namespace Itaris.Modules.Identity.Features.VerifyOtp;

/// <summary>doc 05 A2 body: { challengeId, code, device: { platform, model, fcmToken? } }.</summary>
public sealed record VerifyOtpRequest(Guid ChallengeId, string Code, DeviceInfo Device);

public sealed record DeviceInfo(string Platform, string? Model, string? FcmToken);

/// <summary>
/// doc 05 A2 response: { accessToken, refreshToken, expiresIn, isNewUser, customer }.
/// The customer summary carries what the Identity module authoritatively knows; the richer
/// profile (firstName, preferredLanguage) is populated once the Customers module lands.
/// </summary>
public sealed record VerifyOtpResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    bool IsNewUser,
    CustomerSummary Customer);

public sealed record CustomerSummary(Guid Id, string? FirstName, string PhoneNumber);
