using Microsoft.AspNetCore.Identity;

namespace Itaris.Infrastructure.Auth;

/// <summary>
/// Wraps <see cref="PasswordHasher{TUser}"/> (PBKDF2-HMAC-SHA256). The generic parameter is
/// irrelevant to the algorithm — we use a placeholder so no Identity user model is required.
/// </summary>
public sealed class SecretHasher : ISecretHasher
{
    private sealed class HashSubject;

    private readonly PasswordHasher<HashSubject> _hasher = new();
    private static readonly HashSubject Subject = new();

    public string Hash(string secret) => _hasher.HashPassword(Subject, secret);

    public bool Verify(string hash, string secret) =>
        _hasher.VerifyHashedPassword(Subject, hash, secret) != PasswordVerificationResult.Failed;
}
