using Itaris.Infrastructure.Auth;
using Itaris.Modules.Identity.Domain;
using Itaris.Modules.Identity.Features.Shared;
using Itaris.Modules.Identity.Persistence;
using Itaris.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Identity.Features.VerifyOtp;

/// <summary>
/// doc 05 A2 — verifies an OTP challenge, creates the customer on first login (isNewUser),
/// registers the device, and issues a token pair. Errors: OTP_INVALID, OTP_EXPIRED,
/// OTP_MAX_ATTEMPTS.
/// </summary>
public sealed class VerifyOtpHandler(IdentityDbContext db, AuthTokenIssuer issuer, IClock clock)
{
    private const int MaxAttempts = 5;

    public async Task<Result<VerifyOtpResponse>> HandleAsync(
        VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        var challenge = await db.OtpChallenges
            .FirstOrDefaultAsync(c => c.Id == request.ChallengeId, cancellationToken);

        if (challenge is null || challenge.ConsumedAt is not null)
        {
            return Fail(ErrorCodes.OtpInvalid, "Invalid or already-used challenge.");
        }

        if (challenge.Attempts >= MaxAttempts)
        {
            return Fail(ErrorCodes.OtpMaxAttempts, "Too many attempts. Request a new code.");
        }

        if (clock.UtcNow >= challenge.ExpiresAt)
        {
            return Fail(ErrorCodes.OtpExpired, "This code has expired.");
        }

        if (challenge.CodeHash != OtpCodes.Hash(request.Code))
        {
            challenge.Attempts++;
            await db.SaveChangesAsync(cancellationToken);
            return Fail(ErrorCodes.OtpInvalid, "Incorrect code.");
        }

        challenge.ConsumedAt = clock.UtcNow;

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.PhoneNumber == challenge.PhoneNumber, cancellationToken);
        var isNewUser = user is null;
        if (user is null)
        {
            user = new User { UserType = UserTypes.Customer, PhoneNumber = challenge.PhoneNumber };
            db.Users.Add(user);
        }

        var device = new Device
        {
            UserId = user.Id,
            Platform = request.Device.Platform,
            Model = request.Device.Model,
            FcmToken = request.Device.FcmToken,
        };
        db.Devices.Add(device);

        var tokens = issuer.Issue(
            new TokenRequest(user.Id, Audience: "customer"), device.Id);

        await db.SaveChangesAsync(cancellationToken);

        return Result<VerifyOtpResponse>.Success(new VerifyOtpResponse(
            tokens.AccessToken,
            tokens.RefreshToken,
            tokens.ExpiresInSeconds,
            isNewUser,
            new CustomerSummary(user.Id, FirstName: null, user.PhoneNumber!)));
    }

    private static Result<VerifyOtpResponse> Fail(string code, string message) =>
        Result<VerifyOtpResponse>.Failure(code, message);
}
