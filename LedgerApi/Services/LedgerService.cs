using System.Security.Cryptography;
using System.Text;
using LedgerApi.Contracts.Requests;
using LedgerApi.Data;
using LedgerApi.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LedgerApi.Services
{
    public abstract record PostResult
    {
        public sealed record Created(LedgerTransaction Transaction) : PostResult;

        public sealed record Replayed(LedgerTransaction Transaction) : PostResult;

        public sealed record AccountNotFound : PostResult;

        public sealed record Rejected(RuleViolation Violation) : PostResult;

        public sealed record KeyReuseMismatch : PostResult;
    }

    public class LedgerService(LedgerDbContext context)
    {
        public async Task<PostResult> PostAsync(
            Guid accountId,
            string idempotencyKey,
            PostTransactionRequest req,
            CancellationToken ct
        )
        {
            if (req.Type is not { } type)
            {
                return new PostResult.Rejected(
                    new RuleViolation("type_required", "Transaction type is required.")
                );
            }

            Money requestedAmount = new(req.AmountCents, req.Currency);
            if (LedgerRules.CheckAmount(requestedAmount) is { } amountViolation)
            {
                return new PostResult.Rejected(amountViolation);
            }

            string requestHash = Fingerprint(type, req);

            // grab account by id
            Account? account = await context.Accounts.FirstOrDefaultAsync(
                x => x.Id == accountId,
                ct
            );

            if (account == null)
            {
                return new PostResult.AccountNotFound();
            }

            // check for existing transaction with identical idempotency key
            LedgerTransaction? existingTransaction = await context
                .Transactions.AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.AccountId == accountId && x.IdempotencyKey == idempotencyKey,
                    ct
                );

            if (existingTransaction != null)
            {
                return existingTransaction.RequestHash == requestHash
                    ? new PostResult.Replayed(existingTransaction)
                    : new PostResult.KeyReuseMismatch();
            }

            // ensure matching currencies
            if (req.Currency != account.Currency)
            {
                return new PostResult.Rejected(
                    new RuleViolation("currency_mismatch", "Currencies must be the same.")
                );
            }

            RuleViolation? violation = null;
            switch (type)
            {
                case TransactionType.Refund:
                    // refunds require a corresponding original transaction
                    if (req.OriginalTransactionId == null)
                    {
                        return new PostResult.Rejected(
                            new RuleViolation("original_required", "Original transaction required.")
                        );
                    }

                    LedgerTransaction? originalTransaction = await context
                        .Transactions.AsNoTracking()
                        .FirstOrDefaultAsync(
                            x => x.AccountId == accountId && x.Id == req.OriginalTransactionId,
                            ct
                        );

                    // sum refunds and check
                    var sum = await context
                        .Transactions.Where(t =>
                            t.AccountId == accountId
                            && t.Type == TransactionType.Refund
                            && t.OriginalTransactionId == req.OriginalTransactionId
                        )
                        .SumAsync(t => t.Amount.Cents, ct);
                    violation = LedgerRules.CheckRefund(
                        originalTransaction,
                        accountId,
                        new Money(sum, account.Currency),
                        requestedAmount
                    );
                    break;
                case TransactionType.Payout:
                    var accountBalance = await GetBalanceAsync(accountId, account.Currency, ct);
                    violation = LedgerRules.CheckPayout(accountBalance, requestedAmount);
                    break;
                case TransactionType.Charge:
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Transaction type, {type}, not found.");
            }

            if (violation != null)
            {
                return new PostResult.Rejected(violation);
            }

            LedgerTransaction transaction = new LedgerTransaction
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                Type = type,
                Amount = requestedAmount,
                OriginalTransactionId =
                    type == TransactionType.Refund ? req.OriginalTransactionId : null,
                IdempotencyKey = idempotencyKey,
                RequestHash = requestHash,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            context.Transactions.Add(transaction);

            try
            {
                await context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is SqliteException { SqliteErrorCode: 19 })
            {
                context.Entry(transaction).State = EntityState.Detached;
                LedgerTransaction? replayTransaction = await context
                    .Transactions.AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.AccountId == accountId && x.IdempotencyKey == idempotencyKey,
                        ct
                    );
                if (replayTransaction != null)
                {
                    return replayTransaction.RequestHash == requestHash
                        ? new PostResult.Replayed(replayTransaction)
                        : new PostResult.KeyReuseMismatch();
                }
                else
                {
                    throw;
                }
            }

            return new PostResult.Created(transaction);
        }

        public async Task<List<LedgerTransaction>> GetTransactionsAsync(
            Guid accountId,
            CancellationToken ct
        )
        {
            return await context
                .Transactions.Where(t => t.AccountId == accountId)
                .AsNoTracking()
                .ToListAsync(ct);
        }

        public async Task<Money> GetBalanceAsync(
            Guid accountId,
            string currency,
            CancellationToken ct
        )
        {
            Money accountBalance = new(0, currency);
            foreach (LedgerTransaction t in await GetTransactionsAsync(accountId, ct))
            {
                // signed values get appended to balance
                accountBalance = accountBalance.Add(LedgerRules.Signed(t.Type, t.Amount));
            }
            return accountBalance;
        }

        private static string Fingerprint(TransactionType type, PostTransactionRequest req)
        {
            Guid? original = type == TransactionType.Refund ? req.OriginalTransactionId : null;

            // create deterministic hash
            string canonical = $"{type}|{req.AmountCents}|{req.Currency}|{original}";
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        }
    }
}
