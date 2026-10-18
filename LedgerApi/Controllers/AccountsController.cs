using LedgerApi.Contracts.Requests;
using LedgerApi.Contracts.Responses;
using LedgerApi.Data;
using LedgerApi.Domain;
using LedgerApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LedgerApi.Controllers
{
    [Route("accounts")]
    [ApiController]
    public class AccountsController(LedgerDbContext context, LedgerService service) : ControllerBase
    {
        [HttpPost]
        public async Task<ActionResult<AccountResponse>> Create(
            [FromBody] CreateAccountRequest accountDto,
            CancellationToken ct
        )
        {
            // instantiate a new Account record and add it to the DbSet
            Account account = new Account
            {
                Id = Guid.NewGuid(),
                Name = accountDto.Name,
                Currency = accountDto.Currency,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            context.Accounts.Add(account);
            await context.SaveChangesAsync(ct);

            // return result of create action
            return CreatedAtAction(
                nameof(GetBalance),
                new { id = account.Id },
                new AccountResponse(account.Id, account.Name, account.Currency, account.CreatedAt)
            );
        }

        [HttpGet("{id:guid}/balance")]
        public async Task<ActionResult<BalanceResponse>> GetBalance(Guid id, CancellationToken ct)
        {
            // grab account by id
            Account? account = await context.Accounts.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (account == null)
            {
                return NotFound();
            }

            // account balances are calculated at request time
            Money accountBalance = await service.GetBalanceAsync(id, account.Currency, ct);

            // return calculated balance
            return Ok(new BalanceResponse(account.Id, account.Currency, accountBalance.Cents));
        }

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
                PostResult.Replayed r => Ok(TransactionResponse.From(r.Transaction)),
                PostResult.AccountNotFound => NotFound(),
                PostResult.Rejected re => Problem(
                    detail: re.Violation.Message,
                    statusCode: 422,
                    title: "Business rule violation",
                    extensions: new Dictionary<string, object?> { ["code"] = re.Violation.Code }
                ),
                PostResult.KeyReuseMismatch => Problem("Key reuse mismatch", statusCode: 422),
                _ => throw new InvalidOperationException(),
            };
        }

        [HttpGet("{id:guid}/transactions")]
        public async Task<ActionResult<List<TransactionResponse>>> GetTransactions(
            Guid id,
            CancellationToken ct
        )
        {
            // grab account by id
            Account? account = await context.Accounts.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (account == null)
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
