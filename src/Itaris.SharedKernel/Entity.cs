namespace Itaris.SharedKernel;

/// <summary>
/// Base for all persisted entities (doc 04 Part 8 global conventions):
/// UUIDv7 PK, <c>created_at</c>/<c>updated_at</c> timestamptz in UTC.
/// Optimistic concurrency rides on PostgreSQL <c>xmin</c>, configured centrally
/// by the EF conventions in Itaris.Infrastructure rather than as a property here.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; init; } = Uuid.NewV7();

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
