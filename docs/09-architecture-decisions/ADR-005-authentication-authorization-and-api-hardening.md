# ADR-005: Authentication, Authorization and API Hardening

## Status

Accepted. Implements the design in ADR-002.

## Context

ADR-002 decided *what* identity looks like (separate `User` and `Customer`, OTP-gated claim of an existing customer) but nothing was built: no `[Authorize]` anywhere, no JWT, no CORS, the `Behaviors/`, `Extensions/` and `Abstractions/Security` folders were empty, and every handler validated its input by hand. `CreateOrder` accepted any `customerId` from any caller.

## Decision

### Authentication

- **JWT bearer, HS256, access token only** (60 min, configurable). Claims: `sub`, `email`, `jti`, `role`, `customer_id` (only when linked), `email_verified`.
- Validation pins the algorithm to HS256 (no `alg` confusion / `alg: none`), checks issuer, audience, signature and lifetime, with 30 s clock skew, and does not remap claim names.
- The signing key is a secret. It is **never** in `appsettings.json` (empty there); the app refuses to start without one of at least 32 bytes. Supply it with `Jwt__SigningKey` or user-secrets. `appsettings.Development.json` carries a clearly labelled dev-only key for localhost.
- **Passwords**: PBKDF2-HMAC-SHA256, 600 000 iterations (OWASP minimum), 16-byte random salt, self-describing format (`PBKDF2-SHA256$iterations$salt$hash`) so the cost can be raised later while old hashes still verify. Policy: 8–128 chars, at least one letter and one digit (the cap prevents a cheap PBKDF2 DoS).
- **Login** returns the same 401 for unknown email and wrong password and burns one hash on the unknown-email path so timing does not reveal registered emails. An unverified email may log in (ADR-002); verification gates the *customer link*, not login.
- **Register / ConfirmEmailOtp** follow ADR-002 exactly: no customer with that email -> Customer + User created together and linked; existing Sales-entered customer -> User created *unlinked*, linked only after the emailed code is confirmed. Everything, including sending the email, runs in one transaction (`ITransactionRunner`), so a delivery failure rolls the signup back instead of leaving an account that can never get its code.
- OTP: 6 digits from the OS CSPRNG, 10 min lifetime, single use, only the newest code is valid, constant-time comparison. Every failure mode (unknown user, no live code, wrong code, replay) returns one generic 400.
- **Staff accounts** (Sales, Warehouse, Administrator) are never self-service. The first Administrator is bootstrapped from `Bootstrap__AdminEmail` / `Bootstrap__AdminPassword` at startup (idempotent; a weak password fails startup). After that an Administrator creates staff via `POST /api/users/staff`.

### Authorization

Secure by default: a fallback policy requires an authenticated user, so an endpoint is public only through an explicit `[AllowAnonymous]` (the auth controller and health checks). Every action also names a policy explicitly; `Policies.cs` is the only place the matrix lives, and Administrator belongs to every policy.

| Policy | Roles | Used for |
|---|---|---|
| `AdminOnly` | Administrator | delete customer, create staff users |
| `Sales` | Sales, Administrator | customers (create/update/email/(de)activate), product catalog (create/update/discontinue/reactivate), confirm order, process payment, view payments |
| `Warehouse` | Warehouse, Administrator | restock / reserve stock, ship, deliver |
| `Staff` | Sales, Warehouse, Administrator | read customers, order summary, order payments, low-stock, customer stats |
| `CustomerOrSales` | Customer, Sales, Administrator | create and cancel orders |
| `Authenticated` | any signed-in user | browse products, read / list orders |

**Ownership (Customer role).** Role policies decide who may call an action; for customers the order endpoints additionally compare `currentUser.CustomerId` with the order's customer (`ICurrentUser`, per ADR-002): create (must be for yourself), get, cancel, and list (the customer filter is forced to your own, whatever is sent). Someone else's order is a **404, not a 403**, so ids cannot be probed. A customer whose claim is still pending (no `customer_id` claim) gets a 403 with an explanation. After confirming an OTP the client must log in again, because earlier tokens do not carry the new link.

### Validation pipeline

`ICommandHandler<,>` is wrapped by `ValidationCommandHandlerDecorator` (a plain decorator — SDS §18 still forbids a mediator). Each command may have `IValidator<T>` implementations (`Validator<T>` + `ErrorCollector`); all failures are collected and thrown as one `ValidationException`, surfaced as a ProblemDetails `errors` map grouped by field. Validators check *shape* only (required, format, length, ranges); rules that need the database stay in the handler.

**Scope of this ADR:** the four auth commands use the pipeline (controllers depend on `ICommandHandler<,>`). The ~40 pre-existing handlers are **not** migrated: controllers inject them as concrete types and their unit tests assert the inline checks. Migrating them is mechanical but wide; it is left as a follow-up rather than mixed into this change.

### API hardening

- **Rate limiting** (built-in, per client IP, fixed window): 300 req/min global, **10 req/min on `/api/auth/*`**. The auth limit is what makes guessing a 6-digit OTP impractical, so it is load-bearing, not cosmetic. 429s carry `Retry-After`.
- **CORS** is opt-in via `Cors:AllowedOrigins`; with none configured no origin is allowed and there is never a wildcard.
- **Health**: `/health/live` (process only) and `/health/ready` (database), both anonymous for orchestrators.
- **Errors**: all error bodies are `application/problem+json`. New mappings: 401, 403, application-level 409 (`ConflictException`), `DbUpdateConcurrencyException` -> 409, SQL unique-violation and FK-violation -> 409 (the SQL message is never echoed).
- Enums travel as names in JSON bodies (`JsonStringEnumConverter`); previously a request body only accepted raw integers for enums.

## Consequences

**Positive:** every endpoint has an explicit, test-enforced access rule; a new unprotected action fails `EndpointAuthorizationCoverageTests`; the OTP and credential flows are not enumerable; signup is atomic.

**Known gaps (deliberate, not forgotten):**

- No refresh tokens, no token revocation / logout: a stolen access token is valid until it expires.
- No password reset, no OTP **resend**, no account lockout (only the IP rate limit). The OTP resend gap matters: a user who never receives or loses the code cannot get another.
- `LoggingEmailSender` writes the verification code to the application log. It exists so the flow is testable without an email provider and must be replaced before any real deployment.
- Rate limiting keys on `RemoteIpAddress`. Behind a reverse proxy, forwarded-headers handling must be configured first or all clients share one bucket.
- Customers cannot pay for their own orders (payment stays a Sales action); `CreateOrder` still does not check `Customer.Status`.
- The `users.customer_id` FK is `ON DELETE SET NULL`: deleting a customer record (only possible with no orders) unlinks its login rather than blocking the delete.
- Existing handlers still validate inline (see above).

## Related Documents

- `docs/09-architecture-decisions/ADR-002-user-customer-authentication.md`
- `src/OrderFlow.API/Authorization/Policies.cs`
- `src/OrderFlow.Application/Behaviors/`
- `tests/OrderFlow.IntegrationTests/Security/`
