using LedgerApi.Domain;
using LedgerApi.Contracts;
using System.Net.Http.Json;
using System.Net;

namespace LedgerApi.Tests
{
	public class IdempotencyTests(LedgerApiFactory factory) : IClassFixture<LedgerApiFactory>
	{
		private readonly HttpClient _client = factory.CreateClient();
		private readonly string _idempotencyKey = "charge-1";

		private async Task<HttpResponseMessage> PostTransactionAsync(Guid accountId, Dtos.PostTransactionRequest body, string? key = null)
		{
			var req = new HttpRequestMessage(HttpMethod.Post, $"/accounts/{accountId}/transactions")
			{
				Content = JsonContent.Create(body)
			};
			req.Headers.Add("Idempotency-Key", key);
			return await _client.SendAsync(req);
		}

		private async Task<Guid> CreateAccountAsync(Dtos.CreateAccountRequest body)
		{

			HttpResponseMessage res = await _client.PostAsJsonAsync($"/accounts/", body);
			res.EnsureSuccessStatusCode();
			var account = await res.Content.ReadFromJsonAsync<Dtos.AccountResponse>();
			return account!.Id;
		}

		// #TODO: Fix
		[Fact]
		public async Task Ensure_Transaction_Idempotency()
		{
			// create an account
			var accountId = await CreateAccountAsync(new Dtos.CreateAccountRequest("TestAccount", "CAD"));

			// post original transaction, expect Created response
			Dtos.PostTransactionRequest transactionRequest = new(TransactionType.Charge, 10_000, "CAD", null);
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
			var transactions = await res.Content.ReadFromJsonAsync<List<Dtos.TransactionResponse>>();

			// ensure single transaction and accurate charge amount
			Assert.Single(transactions!);
			Assert.Equal(10_000, transactions![0].AmountCents);
		}

		[Fact]
		public async Task Ensure_Over_Refund_Returns_422()
		{
			// create an account
			var accountId = await CreateAccountAsync(new Dtos.CreateAccountRequest("TestAccount", "CAD"));

			// post original transaction, expect Created response
			Dtos.PostTransactionRequest transactionRequest = new(TransactionType.Charge, 10_000, "CAD", null);
			var res = await PostTransactionAsync(accountId, transactionRequest, "charge-1");
			res.EnsureSuccessStatusCode();
			var transaction = await res.Content.ReadFromJsonAsync<Dtos.TransactionResponse>();
			var transactionId = transaction!.Id;

			// attempt to refund more than original charge
			Dtos.PostTransactionRequest refundRequest = new(TransactionType.Refund, 11_000, "CAD", transactionId);
			res = await PostTransactionAsync(accountId, refundRequest, "refund-1");
			Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
		}

		[Fact]
		public async Task Missing_Idempotency_Key_Returns_400()
		{
			// create an account
			var accountId = await CreateAccountAsync(new Dtos.CreateAccountRequest("TestAccount", "CAD"));

			// post original transaction WITHOUT idempotency-key, expect 400 response
			Dtos.PostTransactionRequest transactionRequest = new(TransactionType.Charge, 10_000, "CAD", null);
			var res = await PostTransactionAsync(accountId, transactionRequest);
			Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
		}

		[Fact]
		public async Task Reusing_Idempotency_Key_With_Different_Body_Returns_422()
		{
			var accountId = await CreateAccountAsync(new Dtos.CreateAccountRequest("TestAccount", "CAD"));

			// original charge with key, expect Created response
			var res = await PostTransactionAsync(accountId, new(TransactionType.Charge, 10_000, "CAD", null), _idempotencyKey);
			Assert.Equal(HttpStatusCode.Created, res.StatusCode);

			// same key, different amount, expect rejection rather than a silent replay of the original
			res = await PostTransactionAsync(accountId, new(TransactionType.Charge, 20_000, "CAD", null), _idempotencyKey);
			Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);

			// only the original charge was recorded
			res = await _client.GetAsync($"/accounts/{accountId}/transactions");
			var transactions = await res.Content.ReadFromJsonAsync<List<Dtos.TransactionResponse>>();
			Assert.Single(transactions!);
			Assert.Equal(10_000, transactions![0].AmountCents);
		}
	}
}
