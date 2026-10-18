using LedgerApi.Domain;

namespace LedgerApi.Tests
{
    /// <summary>
    /// Unit tests for ledger business rules. No database or HTTP involved.
    /// </summary>
    public class LedgerRulesTests
    {
        private const string Currency = "CAD";
        private static readonly Guid AccountId = Guid.NewGuid();

        /// <summary>
        /// Builds a transaction in the test account. Pass accountId to place it in another account.
        /// </summary>
        private static LedgerTransaction Transaction(
            TransactionType type,
            long amount,
            Guid? accountId = null
        ) =>
            new()
            {
                Id = Guid.NewGuid(),
                AccountId = accountId ?? AccountId,
                Type = type,
                Amount = new Money(amount, Currency),
                IdempotencyKey = Guid.NewGuid().ToString(),
                RequestHash = "test-hash", // not under test here; the rules never look at it
                CreatedAt = DateTimeOffset.UtcNow,
            };

        /// <summary>
        /// Shorthand for a charge in the test account.
        /// </summary>
        private static LedgerTransaction Charge(long amount) =>
            Transaction(TransactionType.Charge, amount);

        /// <summary>
        /// Shorthand for a Money value in the test currency.
        /// </summary>
        private static Money Cad(long amount) => new(amount, Currency);

        [Fact]
        public void Refund_equal_to_charge_is_allowed()
        {
            var original = Charge(5_000);
            var result = LedgerRules.CheckRefund(original, AccountId, Cad(0), Cad(5_000));
            Assert.Null(result); // no rule violated
        }

        /// <summary>
        /// With no earlier refunds, a refund cannot exceed the original charge.
        /// </summary>
        [Fact]
        public void Refund_cannot_exceed_charge_amount()
        {
            var original = Charge(5_000);
            var result = LedgerRules.CheckRefund(original, AccountId, Cad(0), Cad(5_001));
            Assert.Equal("refund_exceeds_charge", result?.Code);
        }

        /// <summary>
        /// A refund is checked against what is left after earlier refunds, not the full charge.
        /// </summary>
        [Theory]
        [InlineData(5_000, "refund_exceeds_charge")] // $10 over the remaining $40 refundable amount
        [InlineData(4_001, "refund_exceeds_charge")] // 1 cent over the remaining $40 refundable amount
        [InlineData(4_000, null)] // $40 refund exactly matches remaining refundable amount
        public void Refund_is_checked_against_remaining_amount(
            long requestedAmount,
            string? expectedCode
        )
        {
            var original = Charge(10_000);
            var result = LedgerRules.CheckRefund(
                original,
                AccountId,
                Cad(6_000),
                Cad(requestedAmount)
            );
            Assert.Equal(expectedCode, result?.Code);
        }

        [Fact]
        public void Original_charge_must_exist()
        {
            LedgerTransaction? original = null;
            var result = LedgerRules.CheckRefund(original, AccountId, Cad(0), Cad(5_000));
            Assert.Equal("original_not_found", result?.Code);
        }

        /// <summary>
        /// An original in another account is reported as not found, so its existence is not revealed.
        /// </summary>
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

        /// <summary>
        /// A payout may use the whole balance but not more.
        /// </summary>
        [Theory]
        [InlineData(4_000, null)] // payout below account balance
        [InlineData(5_000, null)] // payout equal to account balance
        [InlineData(5_001, "payout_exceeds_balance")]
        public void Payouts_cannot_exceed_account_balance(
            long requestedAmount,
            string? expectedCode
        )
        {
            var result = LedgerRules.CheckPayout(Cad(5_000), Cad(requestedAmount));
            Assert.Equal(expectedCode, result?.Code);
        }

        /// <summary>
        /// Charges are positive; refunds and payouts are negative.
        /// </summary>
        [Theory]
        [InlineData(TransactionType.Charge, 1_000, 1_000)]
        [InlineData(TransactionType.Refund, 1_000, -1_000)]
        [InlineData(TransactionType.Payout, 1_000, -1_000)]
        public void Signed_applies_by_type(
            TransactionType transactionType,
            long inputAmount,
            long expectedAmount
        )
        {
            var result = LedgerRules.Signed(transactionType, Cad(inputAmount));
            Assert.Equal(expectedAmount, result.Amount);
        }

        /// <summary>
        /// Signing requires a positive amount.
        /// </summary>
        [Theory]
        [InlineData(-1_000)]
        [InlineData(0)]
        public void Signed_rejects_zero_and_negative_amounts(long amount)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                LedgerRules.Signed(TransactionType.Charge, Cad(amount))
            );
        }
    }
}
