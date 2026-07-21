using Itaris.Modules.Loyalty.Domain;

namespace Itaris.Tests.Unit.Loyalty;

/// <summary>
/// Exhaustive tests of the pure earn calculator (doc 06 Phase 3: "preview calculator, pure,
/// unit-tested exhaustively" — rounding, min amount, JOD 3-decimals). The DoD example is 4.500 JOD.
/// </summary>
public class LoyaltyCalculatorTests
{
    private static RuleConfig Points(decimal rate, RoundingMode rounding = RoundingMode.Floor, long minAmountMinor = 0) =>
        new() { PointsPerJod = rate, Rounding = rounding, MinAmountMinor = minAmountMinor };

    private static RuleConfig Stamps(int perVisit = 1, int max = 1, long minAmountMinor = 0) =>
        new() { StampsPerVisit = perVisit, MaxStampsPerVisit = max, MinAmountMinor = minAmountMinor, CardSize = 9 };

    // ---- DoD: 4.500 JOD in both modes ----

    [Fact]
    public void Dod_points_mode_4500_fils_at_1_per_jod_floor_is_4_points()
    {
        var result = LoyaltyCalculator.Calculate(ProgramTypes.Points, Points(1m), amountMinor: 4500);

        Assert.Equal(4, result.PointsEarned);
        Assert.Equal(0, result.StampsEarned);
    }

    [Fact]
    public void Dod_stamps_mode_4500_fils_is_1_stamp()
    {
        var result = LoyaltyCalculator.Calculate(ProgramTypes.Stamps, Stamps(), amountMinor: 4500);

        Assert.Equal(0, result.PointsEarned);
        Assert.Equal(1, result.StampsEarned);
    }

    // ---- Rounding modes on the same 4.5 raw points ----

    [Theory]
    [InlineData(RoundingMode.Floor, 4)]
    [InlineData(RoundingMode.Nearest, 5)] // 4.5 rounds away from zero → 5
    [InlineData(RoundingMode.Ceiling, 5)]
    public void Rounding_modes_resolve_fractional_points(RoundingMode mode, long expected)
    {
        var result = LoyaltyCalculator.Calculate(ProgramTypes.Points, Points(1m, mode), amountMinor: 4500);

        Assert.Equal(expected, result.PointsEarned);
    }

    [Theory]
    [InlineData(1000, 1)]   // exactly 1.000 JOD → 1 point (no fraction)
    [InlineData(1999, 1)]   // 1.999 JOD * 1 = 1.999 → floor 1
    [InlineData(999, 0)]    // 0.999 JOD → floor 0
    [InlineData(0, 0)]      // zero spend → 0
    public void Points_floor_matches_jod_three_decimals(long amountMinor, long expected)
    {
        var result = LoyaltyCalculator.Calculate(ProgramTypes.Points, Points(1m), amountMinor);

        Assert.Equal(expected, result.PointsEarned);
    }

    [Fact]
    public void Points_rate_can_be_fractional_per_jod()
    {
        // 0.5 points per JOD, 10.000 JOD → 5 points
        var result = LoyaltyCalculator.Calculate(ProgramTypes.Points, Points(0.5m), amountMinor: 10_000);

        Assert.Equal(5, result.PointsEarned);
    }

    // ---- Minimum amount ----

    [Fact]
    public void Below_min_amount_earns_nothing_points()
    {
        var result = LoyaltyCalculator.Calculate(ProgramTypes.Points, Points(1m, minAmountMinor: 2000), amountMinor: 1500);

        Assert.Equal(0, result.PointsEarned);
    }

    [Fact]
    public void At_min_amount_earns_points()
    {
        var result = LoyaltyCalculator.Calculate(ProgramTypes.Points, Points(1m, minAmountMinor: 2000), amountMinor: 2000);

        Assert.Equal(2, result.PointsEarned);
    }

    [Fact]
    public void Below_min_amount_earns_no_stamp()
    {
        var result = LoyaltyCalculator.Calculate(ProgramTypes.Stamps, Stamps(minAmountMinor: 2000), amountMinor: 1500);

        Assert.Equal(0, result.StampsEarned);
    }

    // ---- Stamps caps ----

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(3, 1, 1)]  // per-visit capped by max
    [InlineData(2, 5, 2)]  // max above per-visit → per-visit wins
    public void Stamps_are_capped_by_max_per_visit(int perVisit, int max, int expected)
    {
        var result = LoyaltyCalculator.Calculate(ProgramTypes.Stamps, Stamps(perVisit, max), amountMinor: 5000);

        Assert.Equal(expected, result.StampsEarned);
    }

    // ---- Guards ----

    [Fact]
    public void Negative_amount_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LoyaltyCalculator.Calculate(ProgramTypes.Points, Points(1m), amountMinor: -1));
    }

    [Fact]
    public void Unknown_program_type_throws()
    {
        Assert.Throws<ArgumentException>(() =>
            LoyaltyCalculator.Calculate("tiers", Points(1m), amountMinor: 1000));
    }

    [Fact]
    public void Large_amount_does_not_overflow()
    {
        // 1,000,000.000 JOD at 10 points/JOD → 10,000,000 points
        var result = LoyaltyCalculator.Calculate(ProgramTypes.Points, Points(10m), amountMinor: 1_000_000_000);

        Assert.Equal(10_000_000, result.PointsEarned);
    }
}
