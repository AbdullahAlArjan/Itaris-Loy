using Microsoft.Extensions.Caching.Memory;

namespace Itaris.Infrastructure.Auth;

public sealed class InMemoryOtpRateLimiter(IMemoryCache cache) : IOtpRateLimiter
{
    private const int MaxPerIpPerHour = 5;

    public bool TryConsumeForIp(string ipAddress)
    {
        var key = $"otp-ip:{ipAddress}";
        var count = cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            return 0;
        });

        if (count >= MaxPerIpPerHour)
        {
            return false;
        }

        cache.Set(key, count + 1, TimeSpan.FromHours(1));
        return true;
    }
}
