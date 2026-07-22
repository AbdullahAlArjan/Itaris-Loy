using Itaris.Infrastructure.Auth;

namespace Itaris.Modules.Customers.Features.QrToken;

/// <summary>doc 05 B3 response: { qrPayload, expiresInSeconds: 60 }.</summary>
public sealed record QrTokenResponse(string QrPayload, int ExpiresInSeconds);

/// <summary>
/// doc 05 B3 — issues the customer's rotating QR identity token: a short-lived (60s) signed blob the
/// cashier scans. Short TTL + rotation is a fraud control (a screenshot is useless seconds later).
/// </summary>
public sealed class QrTokenHandler(ITokenService tokens)
{
    public const int TtlSeconds = 60;

    public QrTokenResponse Issue(Guid customerId) =>
        new(tokens.CreateQrToken(customerId, TtlSeconds), TtlSeconds);
}
