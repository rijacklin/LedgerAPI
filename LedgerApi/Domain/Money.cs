namespace LedgerApi.Domain
{
    /// <summary>
    /// Immutable monetary value. Amount is stored as a whole number of minor units (e.g. cents)
    /// to avoid floating point error. Arithmetic is only valid between matching currencies.
    /// </summary>
    public record Money(long Amount, string Currency)
    {
        /// <summary>
        /// Throws if the other value is in a different currency.
        /// </summary>
        private void EnsureCurrencyMatch(Money other)
        {
            if (other.Currency != Currency)
            {
                throw new InvalidOperationException(
                    $"Cannot combine {Currency} with {other.Currency}"
                );
            }
        }

        /// <summary>
        /// Returns the sum of this value and another in the same currency.
        /// </summary>
        public Money Add(Money other)
        {
            EnsureCurrencyMatch(other);
            return new Money(Amount + other.Amount, Currency);
        }

        /// <summary>
        /// Returns this value minus another in the same currency. Result may be negative.
        /// </summary>
        public Money Subtract(Money other)
        {
            EnsureCurrencyMatch(other);
            return new Money(Amount - other.Amount, Currency);
        }

        /// <summary>
        /// Returns this value with its sign flipped.
        /// </summary>
        public Money Negate()
        {
            return this with { Amount = -Amount };
        }

        /// <summary>
        /// Returns true if this value is strictly greater than another in the same currency.
        /// </summary>
        public bool IsGreaterThan(Money other)
        {
            EnsureCurrencyMatch(other);
            return Amount > other.Amount;
        }
    }
}
