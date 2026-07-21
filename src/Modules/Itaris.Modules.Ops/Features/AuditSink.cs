using Itaris.Infrastructure.Auditing;
using Itaris.Modules.Ops.Domain;
using Itaris.Modules.Ops.Persistence;

namespace Itaris.Modules.Ops.Features;

/// <summary>Implements <see cref="IAuditSink"/> by appending rows to ops.audit_logs.</summary>
public sealed class AuditSink(OpsDbContext db) : IAuditSink
{
    public async Task WriteAsync(IReadOnlyList<AuditEntry> entries, CancellationToken cancellationToken)
    {
        foreach (var e in entries)
        {
            db.AuditLogs.Add(new AuditLog
            {
                MerchantId = e.MerchantId,
                ActorUserId = e.ActorUserId,
                ActorType = e.ActorType,
                EntityType = e.EntityType,
                EntityId = e.EntityId,
                Action = e.Action,
                PayloadJson = e.AfterSummaryJson,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
