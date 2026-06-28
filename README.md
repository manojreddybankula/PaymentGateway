# Payment Gateway

An ASP.NET Core 8 payment gateway that accepts card payment requests from merchants, forwards them to an acquiring bank simulator, persists the result, and exposes retrieval by payment ID.

## Table of Contents

- [Architecture](#architecture)
- [Prerequisites](#prerequisites)
- [Running the project](#running-the-project)
- [Running the tests](#running-the-tests)
- [API Design](#api-design)
- [Key design decisions](#key-design-decisions)
- [CI](#ci)

## Architecture

Clean Architecture across four projects:

```
src/
  PaymentGateway.Domain/         — Payment entity, enums, domain exceptions. No dependencies.
  PaymentGateway.Service/        — PaymentService, FluentValidation rules, contracts, DTOs.
  PaymentGateway.Infrastructure/ — MongoDB repository, acquiring bank HttpClient + Polly retry.
  PaymentGateway.Api/            — Controllers, DI root, rate limiting, global exception handler.

test/
  PaymentGateway.Domain.Unit.Tests/    — Domain entity invariant tests.
  PaymentGateway.Service.Unit.Tests/   — Validator and PaymentService tests (Moq).
  PaymentGateway.Api.Integration.Tests/ — Full-stack tests via WebApplicationFactory + real MongoDB.
```

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- [Docker](https://www.docker.com/products/docker-desktop) (for running services and integration tests)

## Running the project

### 1. Start all services

```bash
docker compose up --build
```

This starts four containers:

| Container | Purpose | Port |
|---|---|---|
| `payment_gateway_api` | The API | `http://localhost:5001` |
| `mongodb` | MongoDB 7.0 (persistence) | `27017` |
| `bank_simulator` | Mountebank acquiring bank mock | `8080` |
| `seq` | Structured log UI | `http://localhost:8081` |

### 2. Explore the API

Swagger UI is available at **`http://localhost:5001/swagger`**.

#### Process a payment

```bash
curl -X POST http://localhost:5001/api/payments \
  -H "Content-Type: application/json" \
  -d '{
    "cardNumber": "2222405343248871",
    "expiryMonth": 4,
    "expiryYear": 2027,
    "currency": "GBP",
    "amount": 1050,
    "cvv": "123"
  }'
```

#### Retrieve a payment

```bash
curl http://localhost:5001/api/payments/{id}
```

### Bank simulator behaviour

The acquiring bank simulator determines authorization based on the last digit of the card number:

| Last digit | Outcome |
|---|---|
| Odd (1, 3, 5 ...) | Authorized |
| Even (2, 4, 6 ...) | Declined |
| 0 | Bank unavailable (503) |

### Supported currencies

`GBP`, `USD`, `EUR`

## Running the tests

### Unit tests

No infrastructure needed:

```bash
dotnet test --filter "FullyQualifiedName!~Integration"
```

### Integration tests

Integration tests run against a real MongoDB instance. Start the test container first:

```bash
docker compose -f docker-compose.test.yml up -d --wait
```

Then run the tests:

```bash
dotnet test --filter "FullyQualifiedName~Integration"
```

Each test run creates an isolated database (`paymentgateway-test-{guid}`) that is dropped on teardown.

Tear down the test container when done:

```bash
docker compose -f docker-compose.test.yml down
```

### All tests

```bash
docker compose -f docker-compose.test.yml up -d --wait
dotnet test
docker compose -f docker-compose.test.yml down
```

## API Design

Base URL: `http://localhost:5001`

### POST /api/payments

Process a card payment. Validates the request, authorises with the acquiring bank, persists the result, and returns the outcome.

**Request**

```json
{
  "cardNumber":   "2222405343248871",
  "expiryMonth":  4,
  "expiryYear":   2027,
  "currency":     "GBP",
  "amount":       1050,
  "cvv":          "123"
}
```

| Field | Type | Constraints |
|---|---|---|
| `cardNumber` | string | Numeric digits only, 14–19 characters |
| `expiryMonth` | integer | 1–12 |
| `expiryYear` | integer | Must be a future expiry (combined with month) |
| `currency` | string | `GBP`, `USD`, or `EUR` |
| `amount` | integer | Positive integer (minor currency units, e.g. pence) |
| `cvv` | string | Numeric digits only, 3–4 characters |

**Responses**

| HTTP status | Meaning | Body |
|---|---|---|
| `200 OK` | Payment authorised | `PaymentResponse` |
| `400 Bad Request` | Validation failed | `ProblemDetails` with field errors |
| `429 Too Many Requests` | Rate limit exceeded (10 req / 60 s per IP) | `ProblemDetails` + `Retry-After` header |
| `502 Bad Gateway` | Payment declined by bank | `PaymentResponse` with `status: "Declined"` |
| `503 Service Unavailable` | Acquiring bank unreachable | `ProblemDetails` |

**Response body (`PaymentResponse`)**

```json
{
  "id":                "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status":            "Authorized",
  "cardNumberLastFour": "8871",
  "expiryMonth":       4,
  "expiryYear":        2027,
  "currency":          "GBP",
  "amount":            1050
}
```

| Field | Type | Notes |
|---|---|---|
| `id` | UUID | Unique payment identifier for retrieval |
| `status` | string | `Authorized` or `Declined` |
| `cardNumberLastFour` | string | Last 4 digits only — full PAN is never stored or returned |
| `expiryMonth` | integer | |
| `expiryYear` | integer | |
| `currency` | string | |
| `amount` | integer | Minor currency units |

---

### GET /api/payments/{id}

Retrieve a previously processed payment by its ID.

**Path parameter**

| Parameter | Type | Description |
|---|---|---|
| `id` | UUID | The `id` returned from `POST /api/payments` |

**Responses**

| HTTP status | Meaning |
|---|---|
| `200 OK` | Payment found — returns `PaymentResponse` (same shape as POST response) |
| `404 Not Found` | No payment with that ID exists |
| `429 Too Many Requests` | Rate limit exceeded (60 req / 60 s per IP) |

---

### Error response shape

All error responses use RFC 7807 `ProblemDetails`:

```json
{
  "status":   400,
  "title":    "One or more validation errors occurred.",
  "instance": "/api/payments",
  "errors": {
    "CardNumber": ["Card number must be between 14 and 19 characters long."]
  }
}
```

---

## Key design decisions

- **No CQRS** — two use cases (process, retrieve) don't justify the indirection.
- **Validation before persistence** — rejected requests (400) never touch the database or the bank.
- **Card data** — full PAN and CVV are used only transiently to build the bank request; only the last four digits are stored.
- **MongoDB retry** — Polly `WaitAndRetryAsync` (3 attempts, exponential backoff) wraps every repository call for transient connection failures.
- **Rate limiting** — ASP.NET Core's built-in `RateLimiter`: sliding window (10 req/60 s) for writes, fixed window (60 req/60 s) for reads. Both reject immediately with `429` and a `Retry-After` header.
- **Structured logging** — Serilog with Console and Seq sinks. Payment IDs and statuses are logged; card numbers and CVVs are never logged.

See [DESIGN.md](DESIGN.md) for full design rationale.

## CI

GitHub Actions runs on every push to `main` and every pull request. The workflow builds, runs unit tests, spins up the test MongoDB container, runs integration tests, then tears it down. See [`.github/workflows/ci.yml`](.github/workflows/ci.yml).
