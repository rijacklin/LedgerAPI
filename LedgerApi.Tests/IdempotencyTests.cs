using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LedgerApi.Contracts.Requests;
using LedgerApi.Contracts.Responses;
using LedgerApi.Domain;

namespace LedgerApi.Tests
{
    /// <summary>
    /// Integration tests for idempotent transaction posting, run against the real HTTP pipeline.
    /// Each test creates its own account, so tests do not share ledger state.
    /// </summary>
    public class IdempotencyTests(LedgerApiFactory factory) : IClassFixture<LedgerApiFactory>
    {
        private readonly HttpClient _client = factory.CreateClient();
        private readonly string _idempotencyKey = "charge-1";

        /// <summary>
        /// Posts a transaction to the account. A null key omits the Idempotency-Key header.
        /// </summary>
        private async Task<HttpResponseMessage> PostTransactionAsync(
            Guid accountId,
            PostTransactionRequest body,
            string? key = null
        )
        {
            var req = new HttpRequestMessage(HttpMethod.Post, $"/accounts/{accountId}/transactions")
            {
                Content = JsonContent.Create(body),
            };
            req.Headers.Add("Idempotency-Key", key);
            return await _client.SendAsync(req);
        }

        /// <summary>
        /// Creates an account and returns its id.
        /// </summary>
        private async Task<Guid> CreateAccountAsync(CreateAccountRequest body)
        {
            HttpResponseMessage res = await _client.PostAsJsonAsync($"/accounts/", body);
            res.EnsureSuccessStatusCode();
            var account = await res.Content.ReadFromJsonAsync<AccountResponse>();
            return account is null ? throw new InvalidOperationException("create account response had no body.") : account.Id;
        }

        /// <summary>
        /// Posting the same request twice with the same key creates one transaction and replays it
        /// as 200 the second time.
        /// </summary>
        [Fact]
        public async Task Ensure_Transaction_Idempotency()
        {
            // create an account
            var accountId = await CreateAccountAsync(
                new CreateAccountRequest("TestAccount", "CAD")
            );

            // post original transaction, expect Created response
            PostTransactionRequest transactionRequest = new(
                TransactionType.Charge,
                10_000,
                "CAD",
                null
            );
            var res = await PostTransactionAsync(accountId, transactionRequest, _idempotencyKey);
            res.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);

            // post identical replay transaction, expect OK response as transaction should already exist
            res = await PostTransactionAsync(accountId, transactionRequest, _idempotencyKey);
            res.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);

            // list transactions
            res = await _client.GetAsync($"/accounts/{accountId}/transactions");
            res.EnsureSuccessStatusCode();
            var transactions = await res.Content.ReadFromJsonAsync<List<TransactionResponse>>();
            Assert.NotNull(transactions);

            // ensure single transaction and accurate charge amount
            Assert.Single(transactions);
            Assert.Equal(10_000, transactions[0].Amount);
        }

        /// <summary>
        /// A refund greater than the original charge is rejected with 422.
        /// </summary>
        [Fact]
        public async Task Ensure_Over_Refund_Returns_422()
        {
            // create an account
            var accountId = await CreateAccountAsync(
                new CreateAccountRequest("TestAccount", "CAD")
            );

            // post original transaction, expect Created response
            PostTransactionRequest transactionRequest = new(
                TransactionType.Charge,
                10_000,
                "CAD",
                null
            );
            var res = await PostTransactionAsync(accountId, transactionRequest, _idempotencyKey);
            res.EnsureSuccessStatusCode();
            var transaction = await res.Content.ReadFromJsonAsync<TransactionResponse>();
            Assert.NotNull(transaction);
            var transactionId = transaction.Id;

            // attempt to refund more than original charge
            PostTransactionRequest refundRequest = new(
                TransactionType.Refund,
                11_000,
                "CAD",
                transactionId
            );
            res = await PostTransactionAsync(accountId, refundRequest, "refund-1");
            Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
        }

        /// <summary>
        /// A transaction posted without an Idempotency-Key header is rejected with 400.
        /// </summary>
        [Fact]
        public async Task Missing_Idempotency_Key_Returns_400()
        {
            // create an account
            var accountId = await CreateAccountAsync(
                new CreateAccountRequest("TestAccount", "CAD")
            );

            // post original transaction WITHOUT idempotency-key, expect 400 response
            PostTransactionRequest transactionRequest = new(
                TransactionType.Charge,
                10_000,
                "CAD",
                null
            );
            var res = await PostTransactionAsync(accountId, transactionRequest);
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        /// <summary>
        /// Reusing a key with a different payload is rejected with 422 and a machine-readable code,
        /// and does not create a second transaction.
        /// </summary>
        [Fact]
        public async Task Reusing_Idempotency_Key_With_Different_Body_Returns_422()
        {
            var accountId = await CreateAccountAsync(
                new CreateAccountRequest("TestAccount", "CAD")
            );

            // original charge with key, expect Created response
            var res = await PostTransactionAsync(
                accountId,
                new(TransactionType.Charge, 10_000, "CAD", null),
                _idempotencyKey
            );
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);

            // same key, different amount, expect rejection rather than a silent replay of the original
            res = await PostTransactionAsync(
                accountId,
                new(TransactionType.Charge, 20_000, "CAD", null),
                _idempotencyKey
            );
            Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);

            // clients branch on the machine-readable code, not the message
            using var problem = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            Assert.Equal(
                "idempotency_key_reuse",
                problem.RootElement.GetProperty("code").GetString()
            );

            // only the original charge was recorded
            res = await _client.GetAsync($"/accounts/{accountId}/transactions");
            var transactions = await res.Content.ReadFromJsonAsync<List<TransactionResponse>>();
            Assert.NotNull(transactions);
            Assert.Single(transactions);
            Assert.Equal(10_000, transactions[0].Amount);
        }
    }
}
