using System.ComponentModel.DataAnnotations;
using LedgerApi.Domain;

namespace LedgerApi.Contracts.Requests
{
    /// <summary>
    /// Request to post a charge, refund, or payout. Amount is a positive value in minor units
    /// (e.g. cents). OriginalTransactionId is only used, and required, for refunds.
    /// </summary>
    public record PostTransactionRequest(
        [Required] TransactionType? Type,
        [Required, Range(1, long.MaxValue)] long Amount,
        [Required, RegularExpression("^[A-Z]{3}$")] string Currency,
        Guid? OriginalTransactionId
    );
}
