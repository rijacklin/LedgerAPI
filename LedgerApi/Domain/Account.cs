namespace LedgerApi.Domain
{
	public class Account
	{
		public Guid Id { get; set; }
		required public string Name { get; set; }
		required public string Currency { get; set; }
		public DateTimeOffset CreatedAt { get; set; }
	}
}
