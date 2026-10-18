namespace LedgerApi.Domain
{
    /// <summary>
    /// A ledger account holding transactions in a single currency.
    /// The balance is derived at call time from the account's transactions.
    /// </summary>
    public class Account
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public required string Currency { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
