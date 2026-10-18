using LedgerApi.Domain;

namespace LedgerApi.Contracts.Responses
{
    /// <summary>
    /// API representation of an account.
    /// </summary>
    public record AccountResponse(Guid Id, string Name, string Currency, DateTimeOffset CreatedAt)
    {
        public static AccountResponse From(Account a) => new(a.Id, a.Name, a.Currency, a.CreatedAt);
    }
}
