namespace Itaris.Infrastructure.Sms;

/// <summary>
/// Outbound SMS gateway (doc 04: SMS provider behind an interface; real Jordanian
/// gateway integration is a later phase). Phase 1 registers <see cref="FakeSmsProvider"/>.
/// </summary>
public interface ISmsProvider
{
    Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default);
}
