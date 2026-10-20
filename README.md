# <b>LedgerAPI</b>
[![.NET requirement][dotnet-requirement]](https://dotnet.microsoft.com/download/dotnet/10.0)
[![ASP.NET Core][aspnet-core]](https://learn.microsoft.com/aspnet/core)

| Package     | LedgerAPI                                                       |
| ----------- | -----------                                                     |
| Build       | [![CI](https://github.com/rijacklin/LedgerAPI/actions/workflows/ci.yml/badge.svg)](https://github.com/rijacklin/LedgerAPI/actions/workflows/ci.yml) |
| Source      | https://github.com/rijacklin/LedgerAPI                          |
| Stack       | C#, ASP.NET Core, Entity Framework Core, SQLite, xUnit          |
| Keywords    | ledger, payments, idempotency, REST, API, accounting            |

[dotnet-requirement]: https://img.shields.io/badge/.NET-10.0-512BD4
[aspnet-core]: https://img.shields.io/badge/ASP.NET%20Core-10.0-512BD4

LedgerAPI is an ASP.NET Core REST API that represents a simple implementation of a transaction-processing system. This API was developed to solidify my understanding of systems design concepts and is not intended to be used in production. Idempotency is the most significant concept expressed by this API and its design was inspired by [Stripe](https://stripe.com/blog/idempotency).

<br/>

___________

## Key Concepts of the LedgerAPI

- **Idempotent posting:** `POST` transactions are idempotent to ensure the API adheres to the concept of ["effectively-once" semantics](https://www.confluent.io/blog/exactly-once-semantics-are-possible-heres-how-apache-kafka-does-it/). See [`LedgerService.PostAsync`](LedgerApi/Services/LedgerService.cs) and [`IdempotencyTests`](LedgerApi.Tests/IdempotencyTests.cs).
- **Money handling:** Models the concept of 'Money' using a [value object](https://martinfowler.com/eaaCatalog/money.html). See [`Money`](LedgerApi/Domain/Money.cs).
- **Derived balances:** Rather than store account balances on the model they are calculated at request time (at the significant cost of an at-least `O(n)` operation).
- **Rules as data:** When business rules are violated, the API returns descriptive errors instead of throwing exceptions. See [`LedgerRules`](LedgerApi/Domain/LedgerRules.cs).
- **Testing:** Unit tests cover the money and ledger rules and the backoff delays. Integration tests run the real HTTP pipeline against a temporary SQLite database, including a test that holds the database's write lock to force a collision. See [`IdempotencyTests`](LedgerApi.Tests/IdempotencyTests.cs) and [`TransactionRetryTests`](LedgerApi.Tests/TransactionRetryTests.cs).
- **CI:** Every push to `main` and every pull request runs a Release build and the full test suite. See [`ci.yml`](.github/workflows/ci.yml).

## Idempotency

The most important concept explored in the LedgerAPI is idempotency. Similar to how the [Stripe API](https://docs.stripe.com/api/idempotent_requests) is designed, the LedgerAPI uses an idempotency-key field in the header to ensure the API adheres to "effectively once" semantics for transactions. This is crucial because in a real-world payment-processing API, violating "effectively once" semantics on posted transactions would, for example, lead to customers being charged more than once for a given transaction.

As `GET` routes are inherently idempotent (i.e., repeating a request does not change server state; see the [`HttpGet`](LedgerApi/Controllers/AccountsController.cs) routes), only `POST` routes need to be considered for idempotent behaviour in the LedgerAPI. The following routes were designed to adhere to the semantics of idempotency:

| Request | Result |
| ------- | ------ |
| `POST` charge, key `k1` | `201 Created` |
| same request, key `k1` | `200 OK`, original transaction is returned; duplicate transaction is not appended to ledger. |
| different amount, key `k1` | `422`, `idempotency_key_reuse` returned. |
| no `Idempotency-Key` header | `400 Bad Request` |
| write keeps colliding with other requests; retries exhausted | `503 Service Unavailable`; the client should retry with the same key. |

Importantly, the concept of replaying transactions allows a client to safely retry a request for which it has not received a response (e.g., the network dropped the `201 Created` after the charge transaction was already persisted to the ledger).

A unique index on the account and idempotency key guarantees at most one ledger row per key, and each row also stores a fingerprint (i.e., a hash) of the request it was posted with. When a request arrives with an idempotency key that has already been posted to the account ledger, the API compares the stored fingerprint with the fingerprint of the new request. If the fingerprints match, the original transaction is returned with `200 OK` (instead of appending a duplicate transaction); if the fingerprints differ, the request is rejected and the API returns status code `422`, `idempotency_key_reuse`.

It is critical that a replay is checked before applying the business rules. For example, if a client retries a refund of 60 against a charge of 100 whose first refund of 60 was already persisted, `CheckRefund` would count the earlier refund against the retry, find only 40 remaining, and incorrectly reject the retry instead of replaying it.

## Design decisions

### Modeling 'Money'

- **Problem:** Currencies around the world are not expressed equally. Currencies in North America, for example, have the concept of "cents" (i.e., decimal units) whereas a currency like the Japanese Yen does not. How should a transactions API handle differing currencies?
- **Decision:** Amounts are stored as whole numbers (`long`) of a currency's smallest unit, which avoids floating-point rounding error. I roughly followed the guidance from the book "Patterns of Enterprise Application Architecture" to model a 'Money' type for the API. Then, using rules, I restrict the application of comparison operators to currencies of the same type.
- **Tradeoff:** Interestingly, the biggest hurdle I faced was related to naming. In my research, I discovered that real-world payment APIs often solve the problem of currency representation by referring to decimal units as "minor" units. The term is standard (and the code's doc comments use it), but I found that for an API designed to learn systems design, the term "minor" added additional confusion in the names themselves. For this reason, I chose to name the property "amount" and live with the ugly naming conventions, such as `Amount.Amount` to access the (monetary) amount of a transaction.

### Deriving, rather than storing, account balances

- **Problem:** A balance is constantly changing as transactions are appended to the ledger. When a user requests their account balance, how can we be certain they are receiving their exact balance?
- **Decision:** There are two main approaches to this problem: (1) store the calculated account balance in the database, or (2) dynamically calculate the account balance at the time the request is made to the `account/<id>/balance` route. I chose the second solution for two reasons: (1) I don't have to worry about the logistics of keeping each account's stored balance consistently up-to-date, and (2) the operations to calculate a balance at the point of the API request being made is not computationally inefficient for such a simple API.
- **Tradeoff:** Iterating over an account's transactions to calculate a balance is at-least an `O(n)` operation. For a simple API, where accounts have few transactions, this is not an issue. If this were a real-world payments API, where accounts could have millions of transactions, an `O(n)` operation would be far too slow.

### Resolving collisions using exponential backoff

- **Problem:** When multiple idempotent requests are being posted to the API, collisions can occur. SQLite allows one writer at a time, so concurrent writes can collide on the database's write lock. How should repeated, concurrent requests be handled so that they stop colliding and can each reach the API?
- **Decision:** Following what I learned in University from the book "Computer Networking: A Top-Down Approach", I implemented a simple exponential-backoff algorithm that retries a write that hits the database's write lock, waiting a random multiple of 50 ms (between 0 and 2^n − 1 units after the nth failure) before each retry. The randomness stops colliding requests from retrying in lockstep. After 5 attempts the API gives up and returns `503`, and the client should retry with the same idempotency key.
- **Tradeoff:** Retries add bounded latency: at most about 1.3 seconds of waiting across all attempts, then a `503`. SQLite's single-writer lock is what makes collisions likely, so a different database would call for a different strategy. For a real-world payments API, a more complex and robust approach to the concept of exponential-backoff would need to be implemented to not cause unnecessary delays.

### Testing

- **Problem:** A ledger has to behave the same way on every retry and under contention, which is hard to confirm by hand.
- **Decision:** I decided to implement both unit tests for transaction rule violations and the backoff delays, and integration tests for endpoint behaviour. The integration tests host the API in-process with `WebApplicationFactory` and give each run its own temporary SQLite file.
- **Tradeoff:** Using a real database file instead of mocks means the migrations, the unique index and the write-lock behaviour are all exercised, at the cost of slower tests. I did not write a test that sends two requests with the same key at the same instant; the collision that is tested is a held write lock.

## Known limitations

- Different currencies cannot be compared. In a real-world transactions API, the ability to compare currencies (i.e., add, subtract, equate) would be critical. However, the purpose of this API to learn systems design concepts rather than tackling the deep problem of handling currencies.
- No authorization service. With a real-world payments API, the ability to authenticate users would be critical for security.
- Append-only is enforced by the API surface (there are no update or delete routes), not by the database, so nothing stops rows being changed directly. Accounts also cannot be closed or deleted.
- The transaction list is not paginated; it returns every transaction for an account.

## Run it

```bash
dotnet run --project LedgerApi
```

By default, the API listens on localhost port 5052.

## Run the tests

```bash
dotnet test
```

API testing covers both unit tests (Money operations, transaction rules, backoff delays) and integration tests (API endpoints, idempotency, and retry after a write collision).
