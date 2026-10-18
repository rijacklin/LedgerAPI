namespace LedgerApi.Domain
{
    public class Account
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public required string Currency { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
