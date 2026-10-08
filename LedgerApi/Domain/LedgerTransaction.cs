namespace LedgerApi.Domain
{
	public class LedgerTransaction
	{
		public Guid Id {  get; set; }
		public Guid AccountId { get; set; }
		public TransactionType Type { get; set; }
		required public Money Amount { get; set; }
		public Guid? OriginalTransactionId {  get; set; } // only applies to refunds
		required public string IdempotencyKey { get; set; }
		public DateTimeOffset CreatedAt { get; set; }
	}
}
