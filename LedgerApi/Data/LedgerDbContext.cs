using Microsoft.EntityFrameworkCore;
using LedgerApi.Domain;

namespace LedgerApi.Data
{
	public class LedgerDbContext : DbContext
	{
		public LedgerDbContext(DbContextOptions<LedgerDbContext> options) : base(options) { }

		public DbSet<Account> Accounts { get; set; }
		public DbSet<LedgerTransaction> Transactions { get; set; }

		protected override void OnModelCreating(ModelBuilder b)
		{
			base.OnModelCreating(b);

			// ensures Money type's properties become columns on transaction table.
			b.Entity<LedgerTransaction>().ComplexProperty(t => t.Amount);

			// ensures single transaction record, even if identical requests arrive at the same instance.
			b.Entity<LedgerTransaction>().HasIndex(t => new { t.AccountId, t.IdempotencyKey }).IsUnique();

			// convert transaction type enum values to strings.
			b.Entity<LedgerTransaction>().Property(t => t.Type).HasConversion<string>().HasMaxLength(16);
		}
	}
}
