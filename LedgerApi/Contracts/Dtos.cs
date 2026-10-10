using LedgerApi.Domain;
using System.ComponentModel.DataAnnotations;

namespace LedgerApi.Contracts
{
	static public class Dtos
	{
		public record CreateAccountRequest([Required, StringLength(200)] string Name, [Required, RegularExpression("^[A-Z]{3}$")] string Currency);
		public record PostTransactionRequest([Required] TransactionType? Type, [Required, Range(1, long.MaxValue)] long AmountCents, [Required, RegularExpression("^[A-Z]{3}$")] string Currency, Guid? OriginalTransactionId);
		public record AccountResponse(Guid Id, string Name, string Currency, DateTimeOffset CreatedAt)
		{
			public static AccountResponse From(Account a) => new(a.Id, a.Name, a.Currency, a.CreatedAt);
		}
		public record TransactionResponse(Guid Id, Guid AccountId, TransactionType Type, string Currency, long AmountCents, DateTimeOffset CreatedAt)
		{
			public static TransactionResponse From(LedgerTransaction t) => new(t.Id, t.AccountId, t.Type, t.Amount.Currency, t.Amount.Cents, t.CreatedAt);

		}
		public record BalanceResponse(Guid AccountId, string Currency, long BalanceCents);
	}
}
