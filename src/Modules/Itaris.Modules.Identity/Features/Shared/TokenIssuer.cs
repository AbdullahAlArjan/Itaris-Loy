using Itaris.Infrastructure.Auth;
using Itaris.Modules.Identity.Domain;
using Itaris.Modules.Identity.Persistence;
using Itaris.Modules.Identity.PublicApi;

namespace Itaris.Modules.Identity.Features.Shared;

/// <summary>Implements <see cref="ITokenIssuer"/> over the identity schema.</summary>
public sealed class TokenIssuer(IdentityDbContext db, AuthTokenIssuer issuer) : ITokenIssuer
{
    public async Task<TokenPair> IssueAsync(
        TokenRequest request, DeviceRegistration device, CancellationToken cancellationToken)
    {
        var deviceRow = new Device
        {
            UserId = request.UserId,
            Platform = device.Platform,
            Model = device.Model,
            FcmToken = device.FcmToken,
        };
        db.Devices.Add(deviceRow);

        var issued = issuer.Issue(request, deviceRow.Id);
        await db.SaveChangesAsync(cancellationToken);

        return new TokenPair(issued.AccessToken, issued.RefreshToken, issued.ExpiresInSeconds);
    }
}
