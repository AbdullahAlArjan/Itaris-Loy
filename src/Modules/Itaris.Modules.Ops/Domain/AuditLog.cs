using Itaris.SharedKernel;

namespace Itaris.Modules.Ops.Domain;

/// <summary>
/// ops.audit_logs — append-only staff/admin action trail (doc 04 Part 8). Frozen fragments:
/// merchant_id (null=platform), actor_user_id, actor_type, entity_type, payload jsonb, device_id,
/// reason. Rows are never updated or deleted (append-only fraud control, doc 01).
/// </summary>
public sealed class AuditLog : Entity
{
    /// <summary>Null for platform-level actions (admin not scoped to a merchant).</summary>
    public Guid? MerchantId { get; set; }

    public Guid? ActorUserId { get; set; }

    /// <summary>customer | staff | admin.</summary>
    public string? ActorType { get; set; }

    public required string EntityType { get; set; }

    public string? EntityId { get; set; }

    /// <summary>insert | update | delete (interceptor) or a domain action name (handlers).</summary>
    public required string Action { get; set; }

    /// <summary>Summary of what changed (column names only — never secret values). jsonb.</summary>
    public string? PayloadJson { get; set; }

    // PLACEHOLDER(doc04-Part8-clipped): device_id and reason columns are named in doc 04 but not
    // populated yet — reason arrives with handler-level audits (e.g. points adjustment), device_id
    // once cashier device context is threaded through. Reserved as nullable now.
    public Guid? DeviceId { get; set; }

    public string? Reason { get; set; }
}
