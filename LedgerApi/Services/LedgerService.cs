using System.Security.Cryptography;
using System.Text;
using LedgerApi.Contracts.Requests;
using LedgerApi.Data;
using LedgerApi.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LedgerApi.Services
{
    /// <summary>
    /// Outcome of posting a transaction. Closed set: callers should handle every case.
    /// </summary>
    public abstract record PostResult
    {
        private PostResult() { }

        public sealed record Created(LedgerTransaction Transaction) : PostResult;

        /// <summary>The idempotency key and payload matched an earlier request; returns the original.</summary>
        public sealed record Replayed(LedgerTransaction Transaction) : PostResult;

        public sealed record AccountNotFound : PostResult;

        public sealed record Rejected(RuleViolation Violation) : PostResult;

        /// <summary>The idempotency key was already used with a different payload.</summary>
        public sealed record KeyReuseMismatch : PostResult;
    }

    public class LedgerService(LedgerDbContext context)
    {
        /// <summary>
        /// Posts a charge, refund, or payout to an account. Idempotency is enforced using a key.
        /// Retrying a request with the same idempotency key and identical payload returns the original transaction.
        /// Reusing the key with a different payload is rejected.
        /// </summary>
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

            // fingerprint of the payload, used to tell a true replay from key reuse
            string requestHash = Fingerprint(type, req);

            Money requestedAmount = new(req.Amount, req.Currency);
            if (LedgerRules.CheckAmount(requestedAmount) is { } amountViolation)
            {
                return new PostResult.Rejected(amountViolation);
            }

            Account? account = await context.Accounts.FirstOrDefaultAsync(
                x => x.Id == accountId,
                ct
            );
            if (account is null)
            {
                return new PostResult.AccountNotFound();
            }

            // replay existing transaction for this key; after account lookup, before rule checks so retries skip them
            if (
                await FindExistingTransactionAsync(accountId, idempotencyKey, requestHash, ct) is
                { } existing
            )
            {
                return existing;
            }

            // request currency must match the account's currency
            if (
                LedgerRules.CheckCurrency(account.Currency, requestedAmount) is
                { } currencyViolation
            )
            {
                return new PostResult.Rejected(currencyViolation);
            }

            // type-specific rules (refund limits, payout vs. balance)
            if (
                await CheckTypeRulesAsync(type, accountId, account, req, requestedAmount, ct) is
                { } violation
            )
            {
                return new PostResult.Rejected(violation);
            }

            // build the new ledger entry and persist it
            LedgerTransaction transaction = new LedgerTransaction
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                Type = type,
                Amount = requestedAmount,
                OriginalTransactionId = OriginalIdFor(type, req),
                IdempotencyKey = idempotencyKey,
                RequestHash = requestHash,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            return await PersistAsync(transaction, ct);
        }

        /// <summary>
        /// Returns list of transactions associated with an account.
        /// </summary>
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

        /// <summary>
        /// Returns account balance. Balances are calculated at function invocation, rather than being stored in a db field.
        /// </summary>
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

        /// <summary>
        /// Returns the rule violation specific to the transaction type, if any.
        /// </summary>
        private async Task<RuleViolation?> CheckTypeRulesAsync(
            TransactionType type,
            Guid accountId,
            Account account,
            PostTransactionRequest req,
            Money requested,
            CancellationToken ct
        ) =>
            type switch
            {
                TransactionType.Charge => null,
                TransactionType.Refund => await CheckRefundAsync(
                    accountId,
                    account,
                    req,
                    requested,
                    ct
                ),
                TransactionType.Payout => LedgerRules.CheckPayout(
                    await GetBalanceAsync(accountId, account.Currency, ct),
                    requested
                ),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(type),
                    type,
                    $"Transaction type, {type}, not found."
                ),
            };

        /// <summary>
        /// Checks that the original charge exists on this account and that this refund plus earlier refunds don't exceed it.
        /// Returns a rule violation, if any.
        /// </summary>
        private async Task<RuleViolation?> CheckRefundAsync(
            Guid accountId,
            Account account,
            PostTransactionRequest req,
            Money requested,
            CancellationToken ct
        )
        {
            // refunds require a corresponding original transaction
            if (req.OriginalTransactionId is not { } originalId)
            {
                return new RuleViolation("original_required", "Original transaction required.");
            }

            LedgerTransaction? original = await context
                .Transactions.AsNoTracking()
                .FirstOrDefaultAsync(x => x.AccountId == accountId && x.Id == originalId, ct);

            long refundedSoFar = await context
                .Transactions.Where(t =>
                    t.AccountId == accountId
                    && t.Type == TransactionType.Refund
                    && t.OriginalTransactionId == originalId
                )
                .SumAsync(t => t.Amount.Amount, ct);

            return LedgerRules.CheckRefund(
                original,
                accountId,
                new Money(refundedSoFar, account.Currency),
                requested
            );
        }

        /// <summary>
        /// Resolves a previously posted transaction for this idempotency key, if any: a replay when the
        /// request matches, a mismatch when the key was reused with a different payload.
        /// </summary>
        private async Task<PostResult?> FindExistingTransactionAsync(
            Guid accountId,
            string idempotencyKey,
            string requestHash,
            CancellationToken ct
        )
        {
            LedgerTransaction? existing = await context
                .Transactions.AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.AccountId == accountId && x.IdempotencyKey == idempotencyKey,
                    ct
                );

            if (existing is null)
            {
                return null;
            }

            return existing.RequestHash == requestHash
                ? new PostResult.Replayed(existing)
                : new PostResult.KeyReuseMismatch();
        }

        /// <summary>
        /// Inserts the transaction. If a concurrent request with the same idempotency key wins the
        /// race, resolves against the winner's row as a replay or mismatch instead of failing.
        /// </summary>
        private async Task<PostResult> PersistAsync(
            LedgerTransaction transaction,
            CancellationToken ct
        )
        {
            context.Transactions.Add(transaction);

            try
            {
                await context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is SqliteException { SqliteErrorCode: 19 })
            {
                context.Entry(transaction).State = EntityState.Detached;
                if (
                    await FindExistingTransactionAsync(
                        transaction.AccountId,
                        transaction.IdempotencyKey,
                        transaction.RequestHash,
                        ct
                    ) is
                    { } concurrentResult
                )
                {
                    return concurrentResult;
                }
                throw;
            }

            return new PostResult.Created(transaction);
        }

        /// <summary>
        /// Only refunds reference an original transaction; it is ignored for other types.
        /// Shared by persistence and <see cref="Fingerprint"/> so the two cannot disagree.
        /// </summary>
        private static Guid? OriginalIdFor(TransactionType type, PostTransactionRequest req) =>
            type == TransactionType.Refund ? req.OriginalTransactionId : null;

        /// <summary>
        /// Generates a canonical hash to fingerprint the transaction.
        /// </summary>
        private static string Fingerprint(TransactionType type, PostTransactionRequest req)
        {
            Guid? original = OriginalIdFor(type, req);
            string canonical = $"{type}|{req.Amount}|{req.Currency}|{original}";
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        }
    }
}
