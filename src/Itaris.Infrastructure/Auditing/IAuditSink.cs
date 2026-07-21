namespace Itaris.Infrastructure.Auditing;

/// <summary>
/// Persists audit entries. Implemented by the Ops module (which owns ops.audit_logs); the
/// interceptor and middleware depend only on this abstraction, so no module writes another's table.
/// </summary>
public interface IAuditSink
{
    Task WriteAsync(IReadOnlyList<AuditEntry> entries, CancellationToken cancellationToken);
}
