using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Itaris.SharedKernel;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Itaris.Infrastructure.Auth;

public sealed class JwtTokenService(IOptions<JwtOptions> options, IClock clock) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public AccessToken CreateAccessToken(TokenRequest request)
    {
        var now = clock.UtcNow;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, request.UserId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Uuid.NewV7().ToString()),
            new(ItarisClaims.Audience, request.Audience),
        };

        if (request.MerchantId is { } merchantId)
        {
            claims.Add(new Claim(ItarisClaims.MerchantId, merchantId.ToString()));
        }

        if (request.StaffId is { } staffId)
        {
            claims.Add(new Claim(ItarisClaims.StaffId, staffId.ToString()));
        }

        if (request.Role is { } role)
        {
            claims.Add(new Claim(ItarisClaims.Role, role));
        }

        foreach (var branchId in request.BranchIds ?? [])
        {
            claims.Add(new Claim(ItarisClaims.BranchId, branchId.ToString()));
        }

        foreach (var permission in request.Permissions ?? [])
        {
            claims.Add(new Claim(ItarisClaims.Permission, permission));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        var encoded = new JwtSecurityTokenHandler().WriteToken(token);
        return new AccessToken(encoded, _options.AccessTokenMinutes * 60);
    }

    public string GenerateRefreshSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    public string HashRefreshSecret(string secret) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    public string CreateQrToken(Guid customerId, int ttlSeconds)
    {
        var now = clock.UtcNow;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: "qr",
            claims: [new Claim(JwtRegisteredClaimNames.Sub, customerId.ToString())],
            notBefore: now.UtcDateTime,
            expires: now.AddSeconds(ttlSeconds).UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

