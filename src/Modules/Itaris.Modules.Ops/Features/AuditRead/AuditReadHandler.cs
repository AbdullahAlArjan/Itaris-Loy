using Itaris.Modules.Ops.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Itaris.Modules.Ops.Features.AuditRead;

public sealed record AuditLogDto(
    Guid Id, Guid? ActorUserId, string? ActorType, string EntityType, string? EntityId,
    string Action, DateTimeOffset CreatedAt);

public sealed record AuditLogListResponse(IReadOnlyList<AuditLogDto> Items);

/// <summary>doc 05 G7 — merchant-scoped audit trail read, optionally filtered by actor/action.</summary>
public sealed class AuditReadHandler(OpsDbContext db)
{
    public async Task<AuditLogListResponse> ListAsync(
        Guid merchantId, Guid? actor, string? action, CancellationToken cancellationToken)
    {
        var query = db.AuditLogs.AsNoTracking().Where(a => a.MerchantId == merchantId);
        if (actor is { } actorId)
        {
            query = query.Where(a => a.ActorUserId == actorId);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(a => a.Action == action);
        }

        var items = await query
            .OrderByDescending(a => a.Id) // v7 id = chronological
            .Take(100)
            .Select(a => new AuditLogDto(
                a.Id, a.ActorUserId, a.ActorType, a.EntityType, a.EntityId, a.Action, a.CreatedAt))
            .ToListAsync(cancellationToken);

        return new AuditLogListResponse(items);
    }
}
