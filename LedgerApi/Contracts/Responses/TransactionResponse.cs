using LedgerApi.Domain;

namespace LedgerApi.Contracts.Responses
{
    public record TransactionResponse(
        Guid Id,
        Guid AccountId,
        TransactionType Type,
        string Currency,
        long AmountCents,
        DateTimeOffset CreatedAt
    )
    {
        public static TransactionResponse From(LedgerTransaction t) =>
            new(t.Id, t.AccountId, t.Type, t.Amount.Currency, t.Amount.Cents, t.CreatedAt);
    }
}
