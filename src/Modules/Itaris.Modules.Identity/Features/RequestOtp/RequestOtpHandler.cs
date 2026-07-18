using Itaris.Infrastructure.Auth;
using Itaris.Infrastructure.Sms;
using Itaris.Modules.Identity.Domain;
using Itaris.Modules.Identity.Persistence;
using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Itaris.Modules.Identity.Features.RequestOtp;

/// <summary>
/// doc 05 A1 — issues an OTP challenge. Generates a real 6-digit code (a fixed dev-bypass code
/// in the Development environment), stores its hash, and "sends" it via the SMS provider.
/// Enforces doc 05 rate limits: 3/phone/hour (counted from otp_challenges) and 5/ip/hour.
/// </summary>
public sealed class RequestOtpHandler(
    IdentityDbContext db,
    ISmsProvider sms,
    IClock clock,
    IOtpRateLimiter rateLimiter,
    IHostEnvironment environment)
{
    public const int ExpiresInSeconds = 300;
    public const int ResendAfterSeconds = 45;
    private const int MaxPerPhonePerHour = 3;

    public async Task<Result<RequestOtpResponse>> HandleAsync(
        RequestOtpRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(ipAddress) && !rateLimiter.TryConsumeForIp(ipAddress))
        {
            return RateLimited();
        }

        var oneHourAgo = clock.UtcNow.AddHours(-1);
        var recentForPhone = await db.OtpChallenges
            .CountAsync(c => c.PhoneNumber == request.PhoneNumber && c.CreatedAt >= oneHourAgo, cancellationToken);
        if (recentForPhone >= MaxPerPhonePerHour)
        {
            return RateLimited();
        }

        var code = environment.IsDevelopment() ? OtpCodes.DevBypassCode : OtpCodes.Generate();

        var challenge = new OtpChallenge
        {
            PhoneNumber = request.PhoneNumber,
            CodeHash = OtpCodes.Hash(code),
            Purpose = request.Purpose,
            Attempts = 0,
            ExpiresAt = clock.UtcNow.AddSeconds(ExpiresInSeconds),
        };

        db.OtpChallenges.Add(challenge);
        await db.SaveChangesAsync(cancellationToken);

        await sms.SendAsync(request.PhoneNumber, $"Itaris code: {code}", cancellationToken);

        return Result<RequestOtpResponse>.Success(
            new RequestOtpResponse(challenge.Id, ExpiresInSeconds, ResendAfterSeconds));
    }

    private static Result<RequestOtpResponse> RateLimited() =>
        Result<RequestOtpResponse>.Failure(
            ErrorCodes.OtpRateLimited, "Too many OTP requests. Try again later.");
}
