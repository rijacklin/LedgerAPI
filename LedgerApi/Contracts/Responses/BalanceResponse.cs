namespace LedgerApi.Contracts.Responses
{
    /// <summary>
    /// An account's balance at the time of the request, in minor units (e.g. cents) of its currency.
    /// </summary>
    public record BalanceResponse(Guid AccountId, string Currency, long Balance);
}
