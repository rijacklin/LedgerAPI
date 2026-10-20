using LedgerApi.Contracts.Requests;
using LedgerApi.Contracts.Responses;
using LedgerApi.Data;
using LedgerApi.Domain;
using LedgerApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LedgerApi.Controllers
{
    /// <summary>
    /// HTTP endpoints for creating accounts, posting transactions, and reading balances and history.
    /// </summary>
    [Route("accounts")]
    [ApiController]
    public class AccountsController(LedgerDbContext context, LedgerService service) : ControllerBase
    {
        /// <summary>
        /// Creates a new account in the given currency.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<AccountResponse>> Create(
            [FromBody] CreateAccountRequest accountDto,
            CancellationToken ct
        )
        {
            // instantiate a new Account, add it to the DbSet, and persist it.
            Account account = new Account
            {
                Id = Guid.NewGuid(),
                Name = accountDto.Name,
                Currency = accountDto.Currency,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            context.Accounts.Add(account);
            await context.SaveChangesAsync(ct);

            return CreatedAtAction(
                nameof(GetBalance),
                new { id = account.Id },
                new AccountResponse(account.Id, account.Name, account.Currency, account.CreatedAt)
            );
        }

        /// <summary>
        /// Returns the account's current balance, calculated from its transactions.
        /// </summary>
        [HttpGet("{id:guid}/balance")]
        public async Task<ActionResult<BalanceResponse>> GetBalance(Guid id, CancellationToken ct)
        {
            Account? account = await context.Accounts.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (account is null)
            {
                return NotFound();
            }

            Money accountBalance = await service.GetBalanceAsync(id, account.Currency, ct);
            return Ok(new BalanceResponse(account.Id, account.Currency, accountBalance.Amount));
        }

        /// <summary>
        /// Posts a charge, refund, or payout to an account. Requires an Idempotency-Key header.
        /// Returns 201 for a new transaction, 200 for a replay of an earlier request, 404 if the account
        /// does not exist, 422 if a business rule is violated or the key was reused with a different payload,
        /// and 503 if the write kept colliding with other requests; retry with the same Idempotency-Key.
        /// </summary>
        [HttpPost("{id:guid}/transactions")]
        public async Task<IActionResult> PostTransaction(
            Guid id,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            PostTransactionRequest req,
            CancellationToken ct
        )
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
            {
                return Problem("Idempotency-Key header is required.", statusCode: 400);
            }

            PostResult result = await service.PostAsync(id, idempotencyKey, req, ct);

            return result switch
            {
                PostResult.Created c => CreatedAtAction(
                    nameof(GetTransactions),
                    new { id },
                    TransactionResponse.From(c.Transaction)
                ),
                PostResult.Busy => Problem(
                    detail: "The ledger is busy; retry with the same Idempotency-Key.",
                    statusCode: 503,
                    title: "Transaction collision"
                ),
                PostResult.Replayed r => Ok(TransactionResponse.From(r.Transaction)),
                PostResult.AccountNotFound => NotFound(),
                PostResult.Rejected re => Problem(
                    detail: re.Violation.Message,
                    statusCode: 422,
                    title: "Business rule violation",
                    extensions: new Dictionary<string, object?> { ["code"] = re.Violation.Code }
                ),
                PostResult.KeyReuseMismatch => Problem(
                    detail: "Idempotency-Key was already used with a different request.",
                    statusCode: 422,
                    title: "Idempotency key reuse",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "idempotency_key_reuse",
                    }
                ),
                _ => throw new InvalidOperationException(
                    $"Unhandled PostResult: {result.GetType().Name}"
                ),
            };
        }

        /// <summary>
        /// Returns the account's transactions, oldest first.
        /// </summary>
        [HttpGet("{id:guid}/transactions")]
        public async Task<ActionResult<List<TransactionResponse>>> GetTransactions(
            Guid id,
            CancellationToken ct
        )
        {
            Account? account = await context.Accounts.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (account is null)
            {
                return NotFound();
            }

            var transactions = await service.GetTransactionsAsync(id, ct);
            return Ok(
                transactions.Select(TransactionResponse.From).OrderBy(t => t.CreatedAt).ToList()
            );
        }
    }
}
