namespace Itaris.SharedKernel;

/// <summary>
/// Stable machine-readable error codes. Frozen list from doc 05 (API contract) §9.9 —
/// do not add, rename, or remove without a decision-log entry.
/// </summary>
public static class ErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string RateLimited = "RATE_LIMITED";
    public const string ServerError = "SERVER_ERROR";
    public const string IdempotencyConflict = "IDEMPOTENCY_CONFLICT";
    public const string InvalidPhone = "INVALID_PHONE";
    public const string OtpRateLimited = "OTP_RATE_LIMITED";
    public const string OtpInvalid = "OTP_INVALID";
    public const string OtpExpired = "OTP_EXPIRED";
    public const string OtpMaxAttempts = "OTP_MAX_ATTEMPTS";
    public const string SmsDeliveryFailed = "SMS_DELIVERY_FAILED";
    public const string TokenReuseDetected = "TOKEN_REUSE_DETECTED";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string AccountLocked = "ACCOUNT_LOCKED";
    public const string InvalidPin = "INVALID_PIN";
    public const string StaffLocked = "STAFF_LOCKED";
    public const string StaffInactive = "STAFF_INACTIVE";
    public const string AlreadyStaff = "ALREADY_STAFF";
    public const string AlreadyMember = "ALREADY_MEMBER";
    public const string ProgramInactive = "PROGRAM_INACTIVE";
    public const string ProgramLimit = "PROGRAM_LIMIT";
    public const string MembershipPaused = "MEMBERSHIP_PAUSED";
    public const string QrExpired = "QR_EXPIRED";
    public const string QrInvalid = "QR_INVALID";
    public const string QrAlreadyUsed = "QR_ALREADY_USED";
    public const string DuplicateSuspected = "DUPLICATE_SUSPECTED";
    public const string AmountExceedsLimit = "AMOUNT_EXCEEDS_LIMIT";
    public const string InsufficientPoints = "INSUFFICIENT_POINTS";
    public const string RewardOutOfStock = "REWARD_OUT_OF_STOCK";
    public const string RewardInactive = "REWARD_INACTIVE";
    public const string PendingRedemptionExists = "PENDING_REDEMPTION_EXISTS";
    public const string RedemptionExpired = "REDEMPTION_EXPIRED";
    public const string RedemptionAlreadyUsed = "REDEMPTION_ALREADY_USED";
    public const string RedemptionNotFound = "REDEMPTION_NOT_FOUND";
    public const string RefundExceedsRemaining = "REFUND_EXCEEDS_REMAINING";
    public const string AlreadyFullyRefunded = "ALREADY_FULLY_REFUNDED";
    public const string ApprovalRequired = "APPROVAL_REQUIRED";
    public const string AlreadyReported = "ALREADY_REPORTED";
    public const string PhoneInUse = "PHONE_IN_USE";
    public const string MerchantNotFound = "MERCHANT_NOT_FOUND";
}
