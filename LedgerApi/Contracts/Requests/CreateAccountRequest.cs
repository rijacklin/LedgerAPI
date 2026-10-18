using System.ComponentModel.DataAnnotations;

namespace LedgerApi.Contracts.Requests
{
    public record CreateAccountRequest(
        [Required, StringLength(200)] string Name,
        [Required, RegularExpression("^[A-Z]{3}$")] string Currency
    );
}
