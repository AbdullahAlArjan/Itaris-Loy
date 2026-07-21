namespace Itaris.Modules.Loyalty.Domain;

public sealed record PreviewResult(long PointsEarned, int StampsEarned);

/// <summary>
/// Pure points/stamps earn calculation (doc 04: "CalculatePointsPreview — pure function over rules,
/// also used by Transactions"). No I/O, no clock, fully deterministic — the one piece doc 06 says to
/// unit-test exhaustively. Uses <see cref="decimal"/> throughout: money is fils (JOD ×1000), never float.
/// </summary>
public static class LoyaltyCalculator
{
    public static PreviewResult Calculate(string programType, RuleConfig config, long amountMinor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amountMinor);

        // Below the minimum spend, nothing is earned in either mode (doc 04 "min_amount").
        if (amountMinor < config.MinAmountMinor)
        {
            return new PreviewResult(0, 0);
        }

        return programType switch
        {
            ProgramTypes.Points => new PreviewResult(CalculatePoints(config, amountMinor), 0),
            ProgramTypes.Stamps => new PreviewResult(0, CalculateStamps(config)),
            _ => throw new ArgumentException($"Unknown program type '{programType}'.", nameof(programType)),
        };
    }

    private static long CalculatePoints(RuleConfig config, long amountMinor)
    {
        // amountMinor is fils; 1000 fils = 1 JOD. points = (fils / 1000) * pointsPerJod.
        var raw = amountMinor * config.PointsPerJod / 1000m;

        var resolved = config.Rounding switch
        {
            RoundingMode.Floor => Math.Floor(raw),
            RoundingMode.Ceiling => Math.Ceiling(raw),
            RoundingMode.Nearest => Math.Round(raw, MidpointRounding.AwayFromZero),
            _ => Math.Floor(raw),
        };

        return (long)resolved;
    }

    private static int CalculateStamps(RuleConfig config) =>
        Math.Max(0, Math.Min(config.StampsPerVisit, config.MaxStampsPerVisit));
}
