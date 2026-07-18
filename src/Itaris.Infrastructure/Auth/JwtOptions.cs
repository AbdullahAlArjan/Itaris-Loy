namespace Itaris.Infrastructure.Auth;

/// <summary>
/// JWT configuration bound from the "Jwt" section. Access token TTL is 15 min (doc 05);
/// refresh TTL is a chosen value (see docs/decisions.md — not frozen in the specs).
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SigningKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "itaris";
    public string Audience { get; set; } = "itaris-clients";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
}
