namespace Itaris.SharedKernel;

/// <summary>
/// Money as integer minor units (doc 04/05 global convention). JOD uses 3 decimals:
/// 1 JOD = 1000 fils, so JOD 4.500 is <c>AmountMinor = 4500</c>. Never floating point.
/// </summary>
public readonly record struct Money(long AmountMinor, string Currency)
{
    public const string Jod = "JOD";

    public static Money Fils(long amountMinor) => new(amountMinor, Jod);

    public static Money Zero(string currency = Jod) => new(0, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return this with { AmountMinor = checked(AmountMinor + other.AmountMinor) };
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return this with { AmountMinor = checked(AmountMinor - other.AmountMinor) };
    }

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
        {
            throw new InvalidOperationException(
                $"Currency mismatch: {Currency} vs {other.Currency}.");
        }
    }
}
