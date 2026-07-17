namespace Itaris.Modules.Identity.Features.RequestOtp;

/// <summary>doc 05 A1 request body: { phoneNumber, purpose: "login" }.</summary>
public sealed record RequestOtpRequest(string PhoneNumber, string Purpose);

/// <summary>doc 05 A1 response: { challengeId, expiresInSeconds: 300, resendAfterSeconds: 45 }.</summary>
public sealed record RequestOtpResponse(Guid ChallengeId, int ExpiresInSeconds, int ResendAfterSeconds);
