namespace Itaris.SharedKernel;

/// <summary>
/// App-side UUIDv7 generation (doc 04 Part 8: time-ordered PKs generated app-side).
/// </summary>
public static class Uuid
{
    public static Guid NewV7() => Guid.CreateVersion7();

    public static Guid NewV7(DateTimeOffset timestamp) => Guid.CreateVersion7(timestamp);
}
