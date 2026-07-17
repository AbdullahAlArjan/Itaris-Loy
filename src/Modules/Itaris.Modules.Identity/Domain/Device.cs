using Itaris.SharedKernel;

namespace Itaris.Modules.Identity.Domain;

/// <summary>
/// identity.devices — customer/staff devices and FCM tokens, doc 04 Part 8. Frozen fragments:
/// user_id, p… (platform), fcm_token. Shape matches doc 05 A2 device: { platform, model, fcmToken? }.
/// </summary>
public sealed class Device : Entity
{
    public Guid UserId { get; set; }

    public required string Platform { get; set; }

    public string? Model { get; set; }

    public string? FcmToken { get; set; }
}
