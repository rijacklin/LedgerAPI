using LedgerApi.Domain;
using Microsoft.EntityFrameworkCore;

namespace LedgerApi.Data
{
    /// <summary>
    /// EF Core context for accounts and their ledger transactions.
    /// </summary>
    public class LedgerDbContext(DbContextOptions<LedgerDbContext> options) : DbContext(options)
    {
        public DbSet<Account> Accounts { get; set; }
        public DbSet<LedgerTransaction> Transactions { get; set; }

        /// <summary>
        /// Configures how the ledger entities are stored, including the unique index that backs
        /// idempotent posting.
        /// </summary>
        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);

            // ensures Money's properties are mapped as columns on the transaction table
            b.Entity<LedgerTransaction>().ComplexProperty(t => t.Amount);

            // ensures single transaction record per account and idempotency key, even if requests with the same key arrive concurrently
            b.Entity<LedgerTransaction>()
                .HasIndex(t => new { t.AccountId, t.IdempotencyKey })
                .IsUnique();

            // ensures transaction type enum values are stored as strings
            b.Entity<LedgerTransaction>()
                .Property(t => t.Type)
                .HasConversion<string>()
                .HasMaxLength(16);

            // ensures request hash fits a sha-256 hex digest (64 characters)
            b.Entity<LedgerTransaction>().Property(t => t.RequestHash).HasMaxLength(64);
        }
    }
}
