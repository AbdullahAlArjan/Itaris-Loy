namespace Itaris.SharedKernel;

/// <summary>
/// Domain/application error carrying a stable code from <see cref="ErrorCodes"/>.
/// The API layer maps this to the doc 05 error envelope:
/// <c>{ "error": { "code", "message", "details" } }</c> with the given HTTP status.
/// </summary>
public sealed class ApiException(
    int statusCode,
    string code,
    string message,
    IReadOnlyDictionary<string, object?>? details = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
    public IReadOnlyDictionary<string, object?>? Details { get; } = details;

    public static ApiException Validation(string message, IReadOnlyDictionary<string, object?>? details = null) =>
        new(400, ErrorCodes.ValidationError, message, details);

    public static ApiException NotFound(string message) =>
        new(404, ErrorCodes.NotFound, message);

    public static ApiException RateLimited(string code, string message, int retryAfterSeconds) =>
        new(429, code, message, new Dictionary<string, object?> { ["retryAfterSeconds"] = retryAfterSeconds });
}
