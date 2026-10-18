namespace LedgerApi.Contracts.Responses
{
    public record BalanceResponse(Guid AccountId, string Currency, long BalanceCents);
}
