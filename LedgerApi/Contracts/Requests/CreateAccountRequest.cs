using System.ComponentModel.DataAnnotations;

namespace LedgerApi.Contracts.Requests
{
    /// <summary>
    /// Request to create an account. Currency must be a three-letter uppercase code (e.g. CAD).
    /// </summary>
    public record CreateAccountRequest(
        [Required, StringLength(200)] string Name,
        [Required, RegularExpression("^[A-Z]{3}$")] string Currency
    );
}
