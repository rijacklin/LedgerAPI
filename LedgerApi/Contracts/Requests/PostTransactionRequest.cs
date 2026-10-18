using System.ComponentModel.DataAnnotations;
using LedgerApi.Domain;

namespace LedgerApi.Contracts.Requests
{
    public record PostTransactionRequest(
        [Required] TransactionType? Type,
        [Required, Range(1, long.MaxValue)] long AmountCents,
        [Required, RegularExpression("^[A-Z]{3}$")] string Currency,
        Guid? OriginalTransactionId
    );
}
