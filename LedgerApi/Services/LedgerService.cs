using LedgerApi.Contracts;
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

	public class LedgerService
	{
		private readonly LedgerDbContext _context;

		public LedgerService(LedgerDbContext context)
		{
			_context = context;
		}

		public async Task<PostResult> PostAsync(Guid accountId, string idempotencyKey, Dtos.PostTransactionRequest req, CancellationToken ct) {
			Money requestedAmount = new(req.AmountCents, req.Currency);

			// grab account by id
			Account? account = await _context.Accounts.FirstOrDefaultAsync(x => x.Id == accountId, ct);
			if (account == null) return new PostResult.AccountNotFound();

			// check for existing transaction with identical idempotency key
			LedgerTransaction? existingTransaction = await _context.Transactions.AsNoTracking().FirstOrDefaultAsync(x => x.AccountId == accountId && x.IdempotencyKey == idempotencyKey, ct);
			if (existingTransaction != null) return new PostResult.Replayed(existingTransaction);

			// ensure matching currencies
			if (req.Currency != account.Currency)
				return new PostResult.Rejected(new RuleViolation("currency_mismatch", "Currencies must be the same."));

			RuleViolation? violation = null;
			switch (req.Type)
			{
				case TransactionType.Refund:
					// refunds require a corresponding original transaction
					if (req.OriginalTransactionId == null)
						return new PostResult.Rejected(new RuleViolation("original_required", "Original transaction required."));

					LedgerTransaction? originalTransaction = await _context.Transactions.AsNoTracking().FirstOrDefaultAsync(x => x.AccountId == accountId && x.Id == req.OriginalTransactionId, ct);

					// sum refunds and check
					var sum = await _context.Transactions.Where(t => t.AccountId == accountId && t.Type == TransactionType.Refund && t.OriginalTransactionId == req.OriginalTransactionId).SumAsync(t => t.Amount.Cents, ct);
					violation = LedgerRules.CheckRefund(originalTransaction, accountId, new Money(sum, account.Currency), requestedAmount);
					break;
				case TransactionType.Payout:
					var accountBalance = await GetBalanceAsync(accountId, account.Currency, ct);
					violation = LedgerRules.CheckPayout(accountBalance, requestedAmount);
					break;
				case TransactionType.Charge:
					break;
				default:
					throw new ArgumentOutOfRangeException($"Transaction type, {req.Type}, not found.");
			}

			if (violation != null)
				return new PostResult.Rejected(violation);

			LedgerTransaction transaction = new LedgerTransaction {
				Id = Guid.NewGuid(),
				AccountId = accountId,
				Type = req.Type!.Value,
				Amount = requestedAmount,
				OriginalTransactionId = req.Type!.Value == TransactionType.Refund ? req.OriginalTransactionId : null,
				IdempotencyKey = idempotencyKey,
				CreatedAt = DateTimeOffset.UtcNow
			};

			_context.Transactions.Add(transaction);

			try
			{
				await _context.SaveChangesAsync(ct);
			}
			catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19})
			{
				_context.Entry(transaction).State = EntityState.Detached;
				LedgerTransaction? replayTransaction = await _context.Transactions.AsNoTracking().FirstOrDefaultAsync(x => x.AccountId == accountId && x.IdempotencyKey == idempotencyKey, ct);
				if (replayTransaction != null) {
					return new PostResult.Replayed(replayTransaction);
				} else
				{
					throw;
				}
			}

			return new PostResult.Created(transaction);
		}
		public async Task<List<LedgerTransaction>> GetTransactionsAsync(Guid accountId, CancellationToken ct)
		{
			return await _context.Transactions.Where(t => t.AccountId == accountId).AsNoTracking().ToListAsync(ct);
		}
		 
		public async Task<Money> GetBalanceAsync(Guid accountId, string currency, CancellationToken ct)
		{
			Money accountBalance = new(0, currency);
			foreach (LedgerTransaction t in await GetTransactionsAsync(accountId, ct))
			{
				// signed values get appended to balance
				accountBalance = accountBalance.Add(LedgerRules.Signed(t.Type, t.Amount));
			}
			return accountBalance;
		}
	}
}
