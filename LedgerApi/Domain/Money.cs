namespace LedgerApi.Domain
{
	public record Money(long Cents, string Currency)
	{
		private void EnsureCurrencyMatch(Money other) {
			if (other.Currency != this.Currency) throw new InvalidOperationException("Currencies do not match.");
		}

		public Money Add(Money other)
		{
			EnsureCurrencyMatch(other);
			return new Money(this.Cents + other.Cents, this.Currency);
		}

		public Money Subtract(Money other)
		{
			EnsureCurrencyMatch(other);
			return new Money(this.Cents - other.Cents, this.Currency);
		}

		public Money Negate()
		{
			return this with { Cents = -Cents };
		}

		public bool IsGreaterThan(Money other)
		{
			EnsureCurrencyMatch(other);
			return this.Cents > other.Cents;
		}
	}
}
