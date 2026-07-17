using System.Security.Cryptography;
using System.Text;
using Itaris.Infrastructure.Sms;
using Itaris.Modules.Identity.Domain;
using Itaris.Modules.Identity.Persistence;
using Itaris.SharedKernel;

namespace Itaris.Modules.Identity.Features.RequestOtp;

/// <summary>
/// Phase 1 walking-skeleton handler for doc 05 A1. Creates a real otp_challenges row and
/// "sends" the code via the fake SMS provider (visible in logs). Deliberately NOT implemented
/// yet (Phase 2): OTP verification (A2), rate limiting (3/phone/h, 5/ip/h → OTP_RATE_LIMITED),
/// resend throttling. The contract shape and persistence path are what this slice proves.
/// </summary>
public sealed class RequestOtpHandler(IdentityDbContext db, ISmsProvider sms, IClock clock)
{
    public const int ExpiresInSeconds = 300;
    public const int ResendAfterSeconds = 45;

    public async Task<Result<RequestOtpResponse>> HandleAsync(
        RequestOtpRequest request, CancellationToken cancellationToken)
    {
        // PLACEHOLDER(phase2): fixed dev code until real generation + rate limiting land.
        // Doc 01 Part 2 explicitly asks for a dev/test bypass code for pilot demos.
        const string devCode = "000000";

        var challenge = new OtpChallenge
        {
            PhoneNumber = request.PhoneNumber,
            CodeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(devCode))),
            Purpose = request.Purpose,
            Attempts = 0,
            ExpiresAt = clock.UtcNow.AddSeconds(ExpiresInSeconds),
        };

        db.OtpChallenges.Add(challenge);
        await db.SaveChangesAsync(cancellationToken);

        await sms.SendAsync(
            request.PhoneNumber,
            $"Itaris code: {devCode}",
            cancellationToken);

        return Result<RequestOtpResponse>.Success(
            new RequestOtpResponse(challenge.Id, ExpiresInSeconds, ResendAfterSeconds));
    }
}
