using Microsoft.Extensions.Logging;

namespace Itaris.Infrastructure.Sms;

/// <summary>
/// Dev/test SMS provider: logs the message instead of sending it, so OTP codes are
/// visible in console output during local development and pilot demos.
/// Never register this in production.
/// </summary>
public sealed class FakeSmsProvider(ILogger<FakeSmsProvider> logger) : ISmsProvider
{
    public Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("FAKE SMS to {PhoneNumber}: {Message}", phoneNumber, message);
        return Task.CompletedTask;
    }
}
