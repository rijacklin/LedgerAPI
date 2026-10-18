namespace LedgerApi.Domain
{
    public record Money(long Cents, string Currency)
    {
        private void EnsureCurrencyMatch(Money other)
        {
            if (other.Currency != Currency)
                throw new InvalidOperationException("Currencies do not match.");
        }

        public Money Add(Money other)
        {
            EnsureCurrencyMatch(other);
            return new Money(Cents + other.Cents, Currency);
        }

        public Money Subtract(Money other)
        {
            EnsureCurrencyMatch(other);
            return new Money(Cents - other.Cents, Currency);
        }

        public Money Negate()
        {
            return this with { Cents = -Cents };
        }

        public bool IsGreaterThan(Money other)
        {
            EnsureCurrencyMatch(other);
            return Cents > other.Cents;
        }
    }
}
