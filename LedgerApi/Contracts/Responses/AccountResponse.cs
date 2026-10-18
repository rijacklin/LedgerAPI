using LedgerApi.Domain;

namespace LedgerApi.Contracts.Responses
{
    public record AccountResponse(Guid Id, string Name, string Currency, DateTimeOffset CreatedAt)
    {
        public static AccountResponse From(Account a) => new(a.Id, a.Name, a.Currency, a.CreatedAt);
    }
}
