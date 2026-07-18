using Itaris.SharedKernel;

namespace Itaris.Modules.Merchants.Domain;

/// <summary>
/// Staff activation token (doc 04 fragments: merchant_id, staff_member_id, token_hash, accepted_at).
/// NOTE: doc 04 places staff_invites in the identity schema; we keep it in merchants because an
/// invite is fundamentally about staff membership (merchant_id + staff_member_id) and this avoids
/// the root Identity module depending on merchant concepts. Deviation logged in docs/decisions.md.
/// </summary>
public sealed class StaffInvite : Entity
{
    public Guid MerchantId { get; set; }
    public Guid StaffMemberId { get; set; }
    public required string TokenHash { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
}
