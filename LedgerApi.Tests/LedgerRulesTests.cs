using LedgerApi.Domain;

namespace LedgerApi.Tests
{
	public class LedgerRulesTests
	{
		private const string Currency = "CAD";
		private static readonly Guid AccountId = Guid.NewGuid();

		private static LedgerTransaction Transaction(TransactionType type, long cents, Guid? accountId = null) =>
			new()
			{
				Id = Guid.NewGuid(),
				AccountId = accountId ?? AccountId,
				Type = type,
				Amount = new Money(cents, Currency),
				IdempotencyKey = Guid.NewGuid().ToString(),
				CreatedAt = DateTimeOffset.UtcNow,
			};

		private static LedgerTransaction Charge(long cents) => Transaction(TransactionType.Charge, cents);

		private static Money Cad(long cents) => new(cents, Currency);

		// ---- Refunds ----

		[Fact]
		public void Refund_equal_to_charge_is_allowed()
		{
			var original = Charge(5_000);
			var result = LedgerRules.CheckRefund(original, AccountId, Cad(0), Cad(5_000));
			Assert.Null(result); // no rule violated
		}

		[Fact]
		public void Refund_cannot_exceed_charge_amount()
		{
			var original = Charge(5_000);
			var result = LedgerRules.CheckRefund(original, AccountId, Cad(0), Cad(5_001));
			Assert.Equal("refund_exceeds_charge", result?.Code);
		}

		[Theory]
		[InlineData(5_000, "refund_exceeds_charge")]    // $10 over the remaining $40 refundable amount
		[InlineData(4_001, "refund_exceeds_charge")]    // 1 cent over the remaining $40 refundable amount
		[InlineData(4_000, null)]                       // $40 refund exactly matches remaining refundable amount
		public void Refund_is_checked_against_remaining_amount(long requestedCents, string? expectedCode)
		{
			var original = Charge(10_000);
			var result = LedgerRules.CheckRefund(original, AccountId, Cad(6_000), Cad(requestedCents));
			Assert.Equal(expectedCode, result?.Code);
		}

		[Fact]
		public void Original_charge_must_exist()
		{
			LedgerTransaction? original = null;
			var result = LedgerRules.CheckRefund(original, AccountId, Cad(0), Cad(5_000));
			Assert.Equal("original_not_found", result?.Code);
		}

		[Fact]
		public void Original_cannot_belong_to_different_account()
		{
			var original = Charge(10_000);
			var result = LedgerRules.CheckRefund(original, Guid.NewGuid(), Cad(0), Cad(5_000));
			Assert.Equal("original_not_found", result?.Code);
		}

		[Fact]
		public void Original_must_be_charge()
		{
			var original = Transaction(TransactionType.Payout, 10_000);
			var result = LedgerRules.CheckRefund(original, AccountId, Cad(0), Cad(5_000));
			Assert.Equal("original_not_a_charge", result?.Code);
		}

		// ---- Payouts ----

		[Theory]
		[InlineData(4_000, null)]						// payout below account balance
		[InlineData(5_000, null)]						// payout equal to account balance
		[InlineData(5_001, "payout_exceeds_balance")]
		public void Payouts_cannot_exceed_account_balance(long requestedCents, string? expectedCode)
		{
			var result = LedgerRules.CheckPayout(Cad(5_000), Cad(requestedCents));
			Assert.Equal(expectedCode, result?.Code);
		}

		// ---- Signed amounts ----

		[Theory]
		[InlineData(TransactionType.Charge, 1_000, 1_000)]
		[InlineData(TransactionType.Refund, 1_000, -1_000)]
		[InlineData(TransactionType.Payout, 1_000, -1_000)]
		public void Signed_applies_by_type(TransactionType transactionType, long inputCents, long expectedCents)
		{
			var result = LedgerRules.Signed(transactionType, new Money(inputCents, "CAD"));
			Assert.Equal(expectedCents, result.Cents);
		}

		[Theory]
		[InlineData(-1_000)]
		[InlineData(0)]
		public void Signed_rejects_zero_and_negative_amounts(long cents)
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => LedgerRules.Signed(TransactionType.Charge, Cad(cents)));
		}
	}
}
