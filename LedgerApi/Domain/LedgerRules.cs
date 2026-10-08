namespace LedgerApi.Domain
{
	public static class LedgerRules
	{
		public static Money Signed(TransactionType type, Money amount)
		{
			return type switch
			{
				TransactionType.Charge => amount,
				TransactionType.Refund => amount.Negate(),
				TransactionType.Payout => amount.Negate(),
				_ => throw new ArgumentOutOfRangeException(nameof(type)),
			};
		}

		public static RuleViolation? CheckRefund(LedgerTransaction? originalCharge, Guid accountId, Money totalRefundedSoFar, Money requestedRefund)
		{
			if (originalCharge == null || originalCharge.AccountId != accountId) return new RuleViolation("original_not_found", "Cannot find original ledger transaction.");
			if (originalCharge.Type != TransactionType.Charge) return new RuleViolation("original_not_a_charge", "The original transaction was not a charge");
			if (requestedRefund.IsGreaterThan(originalCharge.Amount.Subtract(totalRefundedSoFar)))
			{
				return new RuleViolation("refund_exceeds_charge", "Cannot refund more than the value charged.");
			}
			return null;
		}

		public static RuleViolation? CheckPayout(Money balance, Money requestedPayout)
		{
			if (requestedPayout.IsGreaterThan(balance)) return new RuleViolation("payout_exceeds_balance", "Cannot payout more than the account's balance.");
			return null;
		}
	}
}
