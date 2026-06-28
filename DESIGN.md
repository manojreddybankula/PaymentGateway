# Design Considerations & Assumptions

This document captures the key design decisions and assumptions made while building the payment gateway, beyond what's self-evident from the code.

## Architecture

Clean Architecture, four projects under `src/`:

- **PaymentGateway.Domain** — `Payment` entity, `PaymentStatus`/`Currency` enums, domain exceptions. Zero package dependencies; pure C#.
- **PaymentGateway.Service** — use-case orchestration (`PaymentService`), contracts (`IPaymentsRepository`, `IAcquiringBankClient`), FluentValidation rules, DTOs. Depends only on Domain.
- **PaymentGateway.Infrastructure** — MongoDB persistence (`MongoPaymentsRepository`), the acquiring-bank `HttpClient` implementation with Polly retry. Depends on Service + Domain.
- **PaymentGateway.Api** — controllers, DI composition root, rate limiting, global exception handling.

No CQRS: the gateway has exactly two use cases (process, retrieve), so a single `PaymentService` behind an interface gives the same testability without the indirection overhead.

## Handling the three response types

- **Authorized**: the bank returns a successful authorization; the gateway responds `200 OK` with the payment details and `Status: Authorized`.
- **Declined**: the bank returns a declined authorization; the gateway responds `502 Bad Gateway` with the payment details and `Status: Declined`. The payment is still persisted and retrievable by ID — a decline is a valid business outcome, not an error, but the 502 signals to the merchant that the transaction did not go through.
- **Rejected**: FluentValidation runs *before* the Service layer is ever invoked. If validation fails, the controller returns `400 Bad Request` (`ValidationProblemDetails`) directly — the bank is never called and nothing is persisted. This is why the `Payment` domain entity's `Create` factory explicitly forbids constructing a `Rejected` payment: a rejected request never becomes a `Payment`, it never gets an `Id`, and the response schema for a created/retrieved payment (which only documents `Authorized`/`Declined` as possible `Status` values) stays simple. The 400 response *is* the rejection.

## Card data handling

- The full PAN and CVV are used only transiently to build the outbound bank request; they are never persisted and never logged. `Payment.CardNumberLastFour` is the only retained card fragment.
- Stored as a `string`, not an `int`, so a PAN ending `"0007"` doesn't silently lose its leading zeros.
- `PaymentRequest`/`PaymentResult` DTOs and the `AcquiringBankClient`'s wire-format DTOs are separate types, so a change to the bank's JSON contract (snake_case, `expiry_date` as `MM/yyyy`) can't leak into the Service layer's shape.

## Currency

`Currency` is a closed C# enum (`GBP`, `USD`, `EUR`) rather than a free-text string validated against a configurable list. This structurally guarantees the "no more than 3 currency codes" requirement — adding a fourth currency requires a code change and a conscious decision, not a config edit. The three codes themselves are an arbitrary but reasonable choice; swapping them is a one-line change to `Domain/Enums/Currency.cs`.

## Validation

- FluentValidation owns all request-shape validation (length, numeric-only, range, currency whitelist, future-expiry cross-field check).
- The `Payment.Create` factory in Domain *also* enforces its own minimal invariants (4-digit last-four, valid month, positive amount) via guard clauses. This is deliberate redundancy: Domain should never trust that callers validated correctly, but in practice FluentValidation should always catch bad input first, so these guard clauses should rarely fire outside of programmer error.

## Acquiring bank integration

- The bank simulator's documented contract (`bbyars/mountebank` + `imposters/bank_simulator.ejs`) is treated as a real external dependency, not mocked away in production code — `AcquiringBankClient` is a typed `HttpClient` calling `POST {BaseUrl}/payments`.
- A `503` from the bank, a network failure, or a timeout are all collapsed into one `AcquiringBankUnavailableException`, distinct from a `Declined` payment (which is a normal business outcome, not an error). This exception propagates up to a global `IExceptionHandler` that maps it to `503 Service Unavailable` with a `ProblemDetails` body — the merchant sees a clear "try again" signal rather than a generic 500.
- A 10-second client timeout is set. Transient failures are retried up to **3 times** via a Polly `WaitAndRetryAsync` policy with exponential backoff (2ⁿ seconds: 2 s, 4 s, 8 s). Each retry is logged at `Debug` level with the attempt number, delay, and failure reason. `HandleTransientHttpError()` covers all `5xx` responses (including `503`) and network-level `HttpRequestException`s — so a `503` from the bank is retried up to 3 times before being surfaced as `AcquiringBankUnavailableException`. A client-side timeout (`TaskCanceledException`) bypasses Polly entirely and is caught directly in `AcquiringBankClient`, so timeouts are not retried.

## Persistence

- MongoDB was a conscious choice: payment records are written once and read by ID, with no relational joins or complex queries. A document store maps naturally to this access pattern and avoids schema migration overhead for a schemaless payload like a payment record.
- `PaymentDocument` is a BSON document that maps 1-to-1 to the `Payment` domain entity. `Guid`, `PaymentStatus`, and `Currency` fields are stored as BSON strings (`BsonRepresentation(BsonType.String)`) for human-readability in the database.
- `MongoPaymentsRepository` wraps an `IMongoCollection<PaymentDocument>` registered as a singleton; writes use `InsertOneAsync`, reads use `Find().FirstOrDefaultAsync()`.
- No schema migrations are required — MongoDB is schemaless and the collection is created automatically on first write.
- `docker compose up` brings up MongoDB 7.0 on port `27017` alongside the bank simulator and Seq. A named Docker volume (`mongodb_data`) persists data across restarts.

## Rate Limiting

ASP.NET Core's built-in `RateLimiter` middleware protects both endpoints. Limits are config-driven via `appsettings.json` under `RateLimiting:` and default to:

| Policy | Algorithm | Default limit |
|---|---|---|
| `payments-write` (POST) | Sliding window, 6 segments | 10 requests / 60 s |
| `payments-read` (GET) | Fixed window | 60 requests / 60 s |

- `QueueLimit = 0` on both policies — excess requests are rejected immediately rather than queued.
- Rejected requests receive `429 Too Many Requests` with a `ProblemDetails` body. A `Retry-After` header is included when the rate limiter lease provides that metadata (which the .NET 8 built-in limiters do).
- The `UseRateLimiter()` middleware is placed before authorization and controllers so that rejected requests are terminated before any downstream handler runs.
- A sliding window was chosen for writes over a fixed window because it distributes burst traffic evenly across the window's 6 segments rather than allowing a full burst at the window boundary.

## API shape

- POST returns `200 OK` rather than `201 Created` with a `Location` header. The spec doesn't require resource-creation semantics, and `CreatedAtAction`'s route link-generation turned out to be unreliable in this minimal-hosting setup — `Ok(response)` is simpler and avoids depending on it.
- JSON uses the ASP.NET Core default camelCase casing for the gateway's own API (`cardNumber`, `expiryMonth`, ...). The spec only fixes the wire format for the *simulator's* contract (snake_case); our own merchant-facing API is free to follow normal .NET Web API conventions.
- Enums serialize as strings (`"Authorized"`, `"GBP"`) via a global `JsonStringEnumConverter`, not as integers.

## Logging

Structured logging is provided by **Serilog** with two sinks:

- **Console** — formatted with timestamp, level, source context, and message; useful for local development and container stdout.
- **Seq** — ships structured events to the Seq server (`http://localhost:5341`) for querying and dashboards. The Seq web UI is available at `http://localhost:8081` when running via `docker compose up`.

A bootstrap logger is created before the full host is built so that startup failures (e.g., bad configuration) are captured to the console rather than lost.

`UseSerilogRequestLogging()` replaces ASP.NET Core's default request logging with a single structured log entry per request that includes method, path, status code, and elapsed time.

At the service boundary, payment ID, status, amount, and currency are logged — deliberately never the card number, CVV, or any raw request body.

## Testing strategy

- **Domain.Unit.Tests**: `Payment.Create` invariants, no mocking needed.
- **Service.Unit.Tests**: the FluentValidation validator (via `TestHelper`) and `PaymentService` (via Moq for `IPaymentsRepository`/`IAcquiringBankClient`), covering authorized/declined/bank-unavailable paths and confirming nothing is persisted when the bank call throws.
- **Api.Integration.Tests**: integration tests via `WebApplicationFactory<Program>` against a real MongoDB instance. `CustomWebApplicationFactory` creates a unique database per test run (`paymentgateway-test-{guid}`) and drops it in `Dispose`, keeping tests isolated from each other and from the production database. `IAcquiringBankClient` is swapped for a `FakeAcquiringBankClient` that mirrors the same documented contract as the real Mountebank simulator (odd last digit → authorized, even → declined, `0` → unavailable). **Requires MongoDB running** on `localhost:27018` — start it with `docker compose -f docker-compose.test.yml up -d --wait`.
- All test projects use **NUnit**.

## Out of scope (assumptions)

- No authentication/authorization on the gateway's own API — not mentioned in the requirements.
- No idempotency key / duplicate-submission detection on `POST /payments`.
- No pagination/listing endpoint — only retrieval by `Id`, as specified.
- Single-currency-amount validation only; no cross-currency or Luhn-checksum validation of the card number, since the spec only requires length + numeric-only.
