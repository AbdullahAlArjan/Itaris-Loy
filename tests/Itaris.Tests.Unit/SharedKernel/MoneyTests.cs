using Itaris.SharedKernel;

namespace Itaris.Tests.Unit.SharedKernel;

public class MoneyTests
{
    [Fact]
    public void Fils_creates_jod_minor_units()
    {
        var money = Money.Fils(4500);

        Assert.Equal(4500, money.AmountMinor);
        Assert.Equal("JOD", money.Currency);
    }

    [Fact]
    public void Add_sums_minor_units()
    {
        var result = Money.Fils(4500).Add(Money.Fils(500));

        Assert.Equal(5000, result.AmountMinor);
    }

    [Fact]
    public void Add_rejects_currency_mismatch()
    {
        var jod = Money.Fils(100);
        var other = new Money(100, "USD");

        Assert.Throws<InvalidOperationException>(() => jod.Add(other));
    }

    [Fact]
    public void Subtract_can_go_negative_for_refund_ledger_entries()
    {
        var result = Money.Fils(1000).Subtract(Money.Fils(2500));

        Assert.Equal(-1500, result.AmountMinor);
    }
}
