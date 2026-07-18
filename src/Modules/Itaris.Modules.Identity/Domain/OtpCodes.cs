using System.Security.Cryptography;
using System.Text;

namespace Itaris.Modules.Identity.Domain;

/// <summary>
/// OTP code generation and hashing. Codes are 6-digit; stored only as SHA-256 hashes
/// (short-lived, low-entropy — see docs/decisions.md). The dev-bypass code is honored
/// only when the caller passes it in the Development environment.
/// </summary>
public static class OtpCodes
{
    public const string DevBypassCode = "000000";

    public static string Generate() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public static string Hash(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
}
