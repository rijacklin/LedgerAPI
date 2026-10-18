using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LedgerApi.Tests
{
    /// <summary>
    /// Hosts the API in-process for integration tests, backed by its own temporary SQLite file.
    /// The file is created by migrations at startup and deleted when the factory is disposed.
    /// </summary>
    public class LedgerApiFactory : WebApplicationFactory<Program>
    {
        // create temporary test sqlite db file
        private readonly string _dbPath = Path.Combine(
            Path.GetTempPath(),
            $"ledger-test-{Guid.NewGuid():N}.db"
        );

        /// <summary>
        /// Points the API at the temporary database. Pooling is disabled so SQLite releases the file
        /// when connections close, allowing it to be deleted on dispose.
        /// </summary>
        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.UseSetting("ConnectionStrings:Ledger", $"Data Source={_dbPath};Pooling=False");

        /// <summary>
        /// Deletes the temporary database file.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing)
            {
                return;
            }

            try
            {
                File.Delete(_dbPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // best effort clean-up
                Console.Error.WriteLine(
                    $"Could not delete test database '{_dbPath}': {ex.Message}"
                );
            }
        }
    }
}
