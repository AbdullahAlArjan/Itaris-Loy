namespace Itaris.Infrastructure.Auth;

/// <summary>
/// Hashes and verifies passwords (owner login) and PINs (staff login) using ASP.NET Identity's
/// PBKDF2 hasher. Not the full Identity framework — see docs/decisions.md.
/// </summary>
public interface ISecretHasher
{
    string Hash(string secret);

    bool Verify(string hash, string secret);
}
