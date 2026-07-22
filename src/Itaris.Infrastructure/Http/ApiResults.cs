using Itaris.SharedKernel;

namespace Itaris.Infrastructure.Http;

/// <summary>
/// Bridges <see cref="Result{T}"/> to the HTTP layer: a failure becomes an <see cref="ApiException"/>
/// with the status code that doc 05 semantics imply for its stable error code, which the error
/// envelope middleware then renders as { error: { code, message, details } }.
/// </summary>
public static class ApiResults
{
    public static T OrThrow<T>(this Result<T> result)
    {
        if (result.IsSuccess)
        {
            return result.Value;
        }

        throw new ApiException(
            StatusFor(result.ErrorCode!), result.ErrorCode!, result.ErrorMessage!, result.ErrorDetails);
    }

    public static int StatusFor(string code) => code switch
    {
        ErrorCodes.ValidationError or ErrorCodes.InvalidPhone => 400,
        ErrorCodes.Unauthorized
            or ErrorCodes.OtpInvalid or ErrorCodes.OtpExpired or ErrorCodes.OtpMaxAttempts
            or ErrorCodes.TokenReuseDetected
            or ErrorCodes.InvalidCredentials or ErrorCodes.InvalidPin => 401,
        ErrorCodes.Forbidden
            or ErrorCodes.AccountLocked or ErrorCodes.StaffLocked or ErrorCodes.StaffInactive
            or ErrorCodes.ApprovalRequired => 403,
        ErrorCodes.NotFound or ErrorCodes.MerchantNotFound
            or ErrorCodes.RedemptionNotFound => 404,
        ErrorCodes.IdempotencyConflict
            or ErrorCodes.AlreadyStaff or ErrorCodes.AlreadyMember or ErrorCodes.AlreadyReported
            or ErrorCodes.PhoneInUse or ErrorCodes.ProgramLimit
            or ErrorCodes.PendingRedemptionExists or ErrorCodes.RedemptionAlreadyUsed
            or ErrorCodes.AlreadyFullyRefunded or ErrorCodes.RefundExceedsRemaining
            or ErrorCodes.DuplicateSuspected => 409,
        ErrorCodes.OtpRateLimited or ErrorCodes.RateLimited => 429,
        ErrorCodes.ServerError => 500,
        _ => 400,
    };
}
