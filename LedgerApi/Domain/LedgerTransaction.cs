namespace LedgerApi.Domain
{
    /// <summary>
    /// A single entry in an account's ledger. Entries are never edited or removed; a charge is
    /// reversed by posting a refund.
    /// </summary>
    public class LedgerTransaction
    {
        public Guid Id { get; set; }
        public Guid AccountId { get; set; }
        public TransactionType Type { get; set; }
        public required Money Amount { get; set; }
        public Guid? OriginalTransactionId { get; set; }
        public required string IdempotencyKey { get; set; }
        public required string RequestHash { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
