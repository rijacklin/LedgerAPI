using LedgerApi.Domain;

namespace LedgerApi.Tests;

/// <summary>
/// Unit tests for Money arithmetic, comparison, and its same-currency guard.
/// </summary>
public class MoneyTests
{
    private const string Currency = "CAD";

    /// <summary>
    /// Shorthand for a Money value in the test currency.
    /// </summary>
    private static Money Cad(long amount) => new(amount, Currency);

    /// <summary>
    /// Adding two values in the same currency returns their sum.
    /// </summary>
    [Fact]
    public void Add_returns_sum_in_same_currency()
    {
        var a = Cad(5_000);
        var b = Cad(5_000);
        Assert.Equal(Cad(10_000), a.Add(b));
    }

    /// <summary>
    /// Subtracting returns the difference, including zero and negative results.
    /// </summary>
    [Theory]
    [InlineData(10_000, 4_000, 6_000)]
    [InlineData(10_000, 10_000, 0)]
    [InlineData(10_000, 11_000, -1_000)]
    public void Subtract_returns_difference(long leftAmount, long rightAmount, long expectedAmount)
    {
        var a = Cad(leftAmount);
        var b = Cad(rightAmount);
        Assert.Equal(expectedAmount, a.Subtract(b).Amount);
    }

    /// <summary>
    /// Comparison is strict: equal amounts are not greater than one another.
    /// </summary>
    [Theory]
    [InlineData(5_001, 5_000, true)]
    [InlineData(5_000, 5_000, false)]
    [InlineData(4_999, 5_000, false)]
    public void IsGreaterThan_compares_amounts(long leftAmount, long rightAmount, bool expected)
    {
        var a = Cad(leftAmount);
        var b = Cad(rightAmount);
        Assert.Equal(expected, a.IsGreaterThan(b));
    }

    /// <summary>
    /// Negating flips the sign of the amount, and leaves zero unchanged.
    /// </summary>
    [Theory]
    [InlineData(1_000, -1_000)]
    [InlineData(-1_000, 1_000)]
    [InlineData(0, 0)]
    public void Negate_flips_the_sign(long amount, long expectedAmount)
    {
        var a = Cad(amount);
        Assert.Equal(expectedAmount, a.Negate().Amount);
    }

    [Fact]
    public void Add_with_different_currencies_throws()
    {
        var a = Cad(5_000);
        var b = new Money(4_000, "USD");
        Assert.Throws<InvalidOperationException>(() => a.Add(b));
    }

    [Fact]
    public void Subtract_with_different_currencies_throws()
    {
        var a = Cad(5_000);
        var b = new Money(4_000, "USD");
        Assert.Throws<InvalidOperationException>(() => a.Subtract(b));
    }

    [Fact]
    public void IsGreaterThan_with_different_currencies_throws()
    {
        var a = Cad(5_000);
        var b = new Money(4_000, "USD");
        Assert.Throws<InvalidOperationException>(() => a.IsGreaterThan(b));
    }

    /// <summary>
    /// Money compares by value, so equal amounts in the same currency are equal.
    /// </summary>
    [Fact]
    public void Money_with_same_amount_and_currency_are_equal()
    {
        var a = Cad(1_000);
        var b = Cad(1_000);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Money_with_same_amount_different_currency_are_not_equal()
    {
        var a = Cad(1_000);
        var b = new Money(1_000, "USD");
        Assert.NotEqual(a, b);
    }
}
