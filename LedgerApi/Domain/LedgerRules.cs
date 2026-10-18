namespace LedgerApi.Domain
{
    /// <summary>
    /// Business rules for ledger transactions. Check methods return a rule violation, or null
    /// if the request is allowed.
    /// </summary>
    public static class LedgerRules
    {
        /// <summary>
        /// Returns the amount signed by its effect on the balance: positive for charges, negative
        /// for refunds and payouts. Throws if the amount is not positive or the type is unknown.
        /// </summary>
        public static Money Signed(TransactionType type, Money amount)
        {
            if (amount.Amount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(amount),
                    amount.Amount,
                    "Ledger amounts must be positive and greater than zero."
                );
            }
            return type switch
            {
                TransactionType.Charge => amount,
                TransactionType.Refund => amount.Negate(),
                TransactionType.Payout => amount.Negate(),
                _ => throw new ArgumentOutOfRangeException(nameof(type)),
            };
        }

        /// <summary>
        /// Returns a violation if the requested currency differs from the account's.
        /// </summary>
        public static RuleViolation? CheckCurrency(string accountCurrency, Money requested)
        {
            if (requested.Currency != accountCurrency)
            {
                return new RuleViolation(
                    "currency_mismatch",
                    "Currencies must be the same for comparisons."
                );
            }
            return null;
        }

        /// <summary>
        /// Returns a violation if the amount is not greater than zero.
        /// </summary>
        public static RuleViolation? CheckAmount(Money amount)
        {
            if (amount.Amount <= 0)
            {
                return new RuleViolation(
                    "invalid_amount",
                    "Ledger amounts must be positive and greater than zero."
                );
            }
            return null;
        }

        /// <summary>
        /// Returns a violation if the original is missing, belongs to another account, is not a charge,
        /// or if the refund plus earlier refunds would exceed the original charge.
        /// </summary>
        public static RuleViolation? CheckRefund(
            LedgerTransaction? originalCharge,
            Guid accountId,
            Money totalRefundedSoFar,
            Money requestedRefund
        )
        {
            if (originalCharge is null || originalCharge.AccountId != accountId)
            {
                return new RuleViolation(
                    "original_not_found",
                    "Cannot find original ledger transaction."
                );
            }
            if (originalCharge.Type != TransactionType.Charge)
            {
                return new RuleViolation(
                    "original_not_a_charge",
                    "The original transaction was not a charge"
                );
            }
            if (requestedRefund.IsGreaterThan(originalCharge.Amount.Subtract(totalRefundedSoFar)))
            {
                return new RuleViolation(
                    "refund_exceeds_charge",
                    "Cannot refund more than the value charged."
                );
            }
            return null;
        }

        /// <summary>
        /// Returns a violation if the payout exceeds the account's balance.
        /// </summary>
        public static RuleViolation? CheckPayout(Money balance, Money requestedPayout)
        {
            if (requestedPayout.IsGreaterThan(balance))
            {
                return new RuleViolation(
                    "payout_exceeds_balance",
                    "Cannot payout more than the account's balance."
                );
            }
            return null;
        }
    }
}
