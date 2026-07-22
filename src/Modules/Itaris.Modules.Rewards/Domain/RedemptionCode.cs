using System.Security.Cryptography;

namespace Itaris.Modules.Rewards.Domain;

/// <summary>Generates the 6-char human redemption code (doc 05 §9.8 "K7M3QD"). No ambiguous chars.</summary>
public static class RedemptionCode
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no I/O/0/1

    public static string Generate() =>
        string.Create(6, 0, (span, _) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            }
        });
}
