namespace Itaris.SharedKernel;

/// <summary>
/// Outcome of an application operation: either a value or a stable-coded error.
/// Feature handlers return this; the API layer maps failures to the doc 05 error envelope.
/// </summary>
public sealed class Result<T>
{
    private readonly T? _value;

    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }
    public IReadOnlyDictionary<string, object?>? ErrorDetails { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot read Value of a failed result.");

    private Result(T value)
    {
        _value = value;
        IsSuccess = true;
    }

    private Result(string code, string message, IReadOnlyDictionary<string, object?>? details)
    {
        IsSuccess = false;
        ErrorCode = code;
        ErrorMessage = message;
        ErrorDetails = details;
    }

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(string code, string message, IReadOnlyDictionary<string, object?>? details = null) =>
        new(code, message, details);
}
