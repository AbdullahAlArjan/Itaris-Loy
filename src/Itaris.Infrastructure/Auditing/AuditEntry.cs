namespace Itaris.Infrastructure.Auditing;

/// <summary>
/// One captured staff/admin mutation, en route to ops.audit_logs (doc 04 Part 8 fragments:
/// merchant_id null=platform, actor_user_id, actor_type, entity_type, payload jsonb, reason).
/// </summary>
public sealed record AuditEntry(
    Guid? MerchantId,
    Guid? ActorUserId,
    string? ActorType,
    string EntityType,
    string? EntityId,
    string Action,
    string? AfterSummaryJson);
