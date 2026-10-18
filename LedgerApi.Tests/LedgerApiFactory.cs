using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LedgerApi.Tests
{
    public class LedgerApiFactory : WebApplicationFactory<Program>
    {
        // create temporary test sqlite db file
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"ledger-test-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseSetting("ConnectionStrings:Ledger", $"Data Source={_dbPath};Pooling=False");

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            try
            {
                File.Delete(_dbPath);
            }
            catch (IOException)
            {
                // best effort clean-up
            }
        }
    }
}
