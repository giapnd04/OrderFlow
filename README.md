# OrderFlow

An order management backend built with ASP.NET Core (.NET 9), EF Core and SQL Server. It models the full order lifecycle — products and stock, customers, orders, payments and fulfilment — with role-based access control, inventory reservation and a controlled order state machine.

This is a backend portfolio project: the goal is to go beyond CRUD and show business-rule modelling, data consistency, security and testing in a realistic domain. There is no frontend; the API is the product.

## Features

- **Authentication** — register, login (JWT bearer), email OTP confirmation that links a self-registered account to an existing customer record.
- **Role-based authorization** — `Customer`, `Sales`, `Warehouse`, `Administrator`, enforced through named policies. Customers can only see and cancel their own orders (someone else's order returns 404, not 403).
- **Customers** — create, update, change email, deactivate / reactivate, delete, per-customer order stats.
- **Products & inventory** — catalog management, discontinue / reactivate, restock, reserve stock, low-stock report. Stock is tracked as `StockQuantity` and `ReservedQuantity`; available = stock − reserved.
- **Orders** — create (reserves stock per line), confirm, cancel, ship, deliver, list with filters, status summary. Unit price is snapshotted on the order item so later price changes never alter history.
- **Payments** — process a payment attempt against an order, list payments, per-order payment summary.
- **API hardening** — fallback "authenticated" policy, rate limiting (stricter on `/api/auth/*`), opt-in CORS, ProblemDetails error responses, health checks.

## Order lifecycle

```text
PendingPayment ──► Paid ──► Confirmed ──► Shipped ──► Delivered
      │                          │
      └──────► Cancelled ◄───────┘
```

- Invalid transitions are rejected by the domain model, not by controllers.
- Cancelling before payment releases the stock reservation; cancelling after payment returns the stock.
- `Paid` is a locked state: an order must be confirmed before it can be cancelled.
- Rationale is recorded in [ADR-003](docs/09-architecture-decisions/ADR-003-order-lifecycle.md) and [ADR-004](docs/09-architecture-decisions/ADR-004-inventory-reservation.md).

## Architecture

Clean Architecture with the dependency rule pointing inward:

```text
src/
├── OrderFlow.Domain          entities, enums, domain exceptions, state-transition rules
├── OrderFlow.Application     use cases (command/query handlers), validators, abstractions
├── OrderFlow.Infrastructure  EF Core DbContext, configurations, migrations, repositories, security services
└── OrderFlow.API             controllers, authorization policies, middleware, Program.cs
tests/
├── OrderFlow.UnitTests         handler and domain tests
└── OrderFlow.IntegrationTests  API security / authorization coverage via WebApplicationFactory
docs/                         project definition, SRS, SDS, ADRs, notes
```

Notable design choices:

- One handler per use case under `Application/Features/<Area>/<UseCase>`; no mediator library.
- Command validation runs through a decorator that collects all errors into one ProblemDetails response.
- `Product` carries a `RowVersion` for optimistic concurrency; a concurrency conflict surfaces as HTTP 409.
- Order and payment statuses are stored as strings with database CHECK constraints.

## Tech stack

| Area | Choice |
|---|---|
| Runtime | .NET 9, ASP.NET Core Web API |
| Data | EF Core 9, SQL Server (LocalDB for development), code-first migrations |
| Auth | JWT bearer (HS256), PBKDF2-HMAC-SHA256 password hashing (600k iterations) |
| API docs | built-in OpenAPI (`MapOpenApi`) |
| Testing | xUnit, `Microsoft.AspNetCore.Mvc.Testing` |

## API overview

| Area | Endpoints |
|---|---|
| Auth | `POST /api/auth/register`, `/login`, `/confirm-email` |
| Users | `POST /api/users/staff` (Administrator only) |
| Customers | `GET/POST /api/customers`, `GET/PUT /api/customers/{id}`, `PUT .../email`, `POST .../deactivate`, `POST .../reactivate`, `GET .../order-stats`, `DELETE .../{id}` |
| Products | `GET/POST /api/products`, `GET/PUT /api/products/{id}`, `POST .../discontinue`, `.../reactivate`, `.../restock`, `.../reserve`, `GET /api/products/low-stock` |
| Orders | `GET/POST /api/orders`, `GET /api/orders/{id}`, `POST .../confirm`, `.../cancel`, `.../ship`, `.../deliver`, `GET /api/orders/summary` |
| Payments | `POST /api/orders/{id}/payments`, `GET /api/orders/{id}/payments`, `.../payments/summary`, `GET /api/payments`, `GET /api/payments/{id}` |
| Health | `GET /health/live`, `GET /health/ready` |

Role-to-endpoint matrix: see [ADR-005](docs/09-architecture-decisions/ADR-005-authentication-authorization-and-api-hardening.md). Ready-made requests are in [OrderFlow.API.http](src/OrderFlow.API/OrderFlow.API.http).

## Running locally

Prerequisites: .NET 9 SDK and SQL Server LocalDB (or change the connection string).

```bash
# apply migrations
dotnet ef database update --project src/OrderFlow.Infrastructure --startup-project src/OrderFlow.API

# run the API
dotnet run --project src/OrderFlow.API
```

Development settings in `appsettings.Development.json` use LocalDB and a clearly labelled dev-only JWT signing key. In any other environment supply your own secrets:

| Setting | Purpose |
|---|---|
| `ConnectionStrings__OrderFlow` | SQL Server connection string |
| `Jwt__SigningKey` | JWT signing key, at least 32 bytes (the app refuses to start without it) |
| `Bootstrap__AdminEmail`, `Bootstrap__AdminPassword` | creates the first Administrator at startup |
| `Cors__AllowedOrigins` | browser origins allowed to call the API (none by default) |

## Testing

```bash
dotnet test
```

- **Unit tests** cover the application handlers (auth, customers, products, orders, payments), validation pipeline and domain rules.
- **Integration tests** exercise the real HTTP pipeline, including a test that fails if any endpoint is left without an authorization policy.
- **Database tests** run against a real SQL Server with the production migrations applied: a Testcontainers container when Docker is available, or the database in `ORDERFLOW_TEST_CONNECTION` (its name must contain "Test", e.g. `Server=(localdb)\MSSQLLocalDB;Database=OrderFlow_IntegrationTests;Trusted_Connection=True;TrustServerCertificate=True`). See `docs/06-testing.md`.

## Documentation

| Document | Content |
|---|---|
| [Project definition](docs/01-project-definition.md) | problem, users, scope, MVP |
| [SRS](docs/02-SRS.md) | requirements and business rules |
| [SDS](docs/03-SDS.md) | system design |
| [ADR-001](docs/09-architecture-decisions/ADR-001-clean-architecture.md) | Clean Architecture |
| [ADR-002](docs/09-architecture-decisions/ADR-002-user-customer-authentication.md) | User / Customer identity model |
| [ADR-003](docs/09-architecture-decisions/ADR-003-order-lifecycle.md) | Order lifecycle |
| [ADR-004](docs/09-architecture-decisions/ADR-004-inventory-reservation.md) | Inventory reservation |
| [ADR-005](docs/09-architecture-decisions/ADR-005-authentication-authorization-and-api-hardening.md) | Auth, authorization, API hardening |

## Known limitations / roadmap

- Stock reservations do not expire: an unpaid order holds its reservation until cancelled.
- No refund flow and no real payment-gateway integration; payments are simulated.
- Most pre-existing handlers do not yet use the validation decorator (only the auth commands do).
- Dockerfile and GitHub Actions CI are planned but not added yet.
- Refresh tokens, audit log and Redis caching are out of the current scope.

## Author

Giap Nguyen — [ndgiap2004@gmail.com](mailto:ndgiap2004@gmail.com)
