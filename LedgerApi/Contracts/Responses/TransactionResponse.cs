using LedgerApi.Domain;

namespace LedgerApi.Contracts.Responses
{
    /// <summary>
    /// API representation of a ledger transaction. Amount is a positive value in minor units;
    /// Type determines its effect on the balance.
    /// </summary>
    public record TransactionResponse(
        Guid Id,
        Guid AccountId,
        TransactionType Type,
        string Currency,
        long Amount,
        DateTimeOffset CreatedAt
    )
    {
        public static TransactionResponse From(LedgerTransaction t) =>
            new(t.Id, t.AccountId, t.Type, t.Amount.Currency, t.Amount.Amount, t.CreatedAt);
    }
}
