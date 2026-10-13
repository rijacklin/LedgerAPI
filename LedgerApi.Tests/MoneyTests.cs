using LedgerApi.Domain;

namespace LedgerApi.Tests;

public class MoneyTests
{
	private const string Currency = "CAD";

	private static Money Cad(long cents) => new(cents, Currency);

	// ---- Add ----

	[Fact]
	public void Add_returns_sum_in_same_currency()
	{
		var a = Cad(5_000);
		var b = Cad(5_000);
		Assert.Equal(Cad(10_000), a.Add(b));
	}

	// ---- Subtract ----

	[Theory]
	[InlineData(10_000, 4_000, 6_000)]
	[InlineData(10_000, 10_000, 0_000)]
	[InlineData(10_000, 11_000, -1_000)]
	public void Subtract_returns_difference(long leftCents, long rightCents, long expectedCents)
	{
		var a = Cad(leftCents);
		var b = Cad(rightCents);
		Assert.Equal(expectedCents, a.Subtract(b).Cents);
	}

	// ---- IsGreaterThan ----

	[Theory]
	[InlineData(5_001, 5_000, true)]
	[InlineData(5_000, 5_000, false)]
	[InlineData(4_999, 5_000, false)]
	public void IsGreaterThan_compares_cents(long leftCents, long rightCents, bool expected)
	{
		var a = Cad(leftCents);
		var b = Cad(rightCents);
		Assert.Equal(expected, a.IsGreaterThan(b));
	}

	// ---- Negate ----

	[Theory]
	[InlineData(1_000, -1_000)]
	[InlineData(-1_000, 1_000)]
	[InlineData(0, 0)]
	public void Negate_flips_the_sign(long cents, long expectedCents)
	{
		var a = Cad(cents);
		Assert.Equal(expectedCents, a.Negate().Cents);
	}

	// ---- Currency guard ----

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

	// ---- Immutability and equality (record behaviour) ----

	[Fact]
	public void Money_with_same_cents_and_currency_are_equal()
	{
		var a = Cad(1_000);
		var b = Cad(1_000);
		Assert.Equal(a, b);	// records compare by value.
	}

	[Fact]
	public void Money_with_same_cents_different_currency_are_not_equal()
	{
		var a = Cad(1_000);
		var b = new Money(1_000, "USD");
		Assert.NotEqual(a, b);
	}
}
