using System.Net;
using System.Net.Http.Json;
using LedgerApi.Contracts.Requests;
using LedgerApi.Contracts.Responses;
using LedgerApi.Domain;
using Microsoft.Data.Sqlite;

namespace LedgerApi.Tests;

/// <summary>
/// Integration test for retrying a collided write: another connection holds the database's write lock,
/// then lets go, and the post succeeds on a retry without creating a duplicate.
/// </summary>
public class TransactionRetryTests(LedgerApiFactory factory) : IClassFixture<LedgerApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    /// <summary>
    /// A post that collides with a held write lock is retried and still creates exactly one transaction.
    /// </summary>
    [Fact]
    public async Task Post_retries_after_collision_and_creates_one_transaction()
    {
        // create an account
        var created = await _client.PostAsJsonAsync(
            "/accounts/",
            new CreateAccountRequest("TestAccount", "CAD")
        );
        created.EnsureSuccessStatusCode();
        var account = await created.Content.ReadFromJsonAsync<AccountResponse>();
        Assert.NotNull(account);

        // another connection takes the database's write lock
        using var blocker = new SqliteConnection(factory.ConnectionString);
        blocker.Open();
        using (var begin = blocker.CreateCommand())
        {
            begin.CommandText = "BEGIN IMMEDIATE";
            begin.ExecuteNonQuery();
        }

        // post a charge: the first attempt waits out the 1s timeout and collides
        var req = new HttpRequestMessage(HttpMethod.Post, $"/accounts/{account.Id}/transactions")
        {
            Content = JsonContent.Create(
                new PostTransactionRequest(TransactionType.Charge, 10_000, "CAD", null)
            ),
        };
        req.Headers.Add("Idempotency-Key", "retry-1");
        Task<HttpResponseMessage> posting = _client.SendAsync(req);

        // release the lock while the request is still retrying
        await Task.Delay(1_500);
        using (var rollback = blocker.CreateCommand())
        {
            rollback.CommandText = "ROLLBACK";
            rollback.ExecuteNonQuery();
        }

        var res = await posting;
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        // exactly one transaction was recorded
        var list = await _client.GetAsync($"/accounts/{account.Id}/transactions");
        list.EnsureSuccessStatusCode();
        var transactions = await list.Content.ReadFromJsonAsync<List<TransactionResponse>>();
        Assert.NotNull(transactions);
        Assert.Single(transactions);
    }
}
