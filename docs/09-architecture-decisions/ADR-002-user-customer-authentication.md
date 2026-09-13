# ADR-002: User / Customer Identity and Authentication Model

## Status

Accepted

## Context

Two source documents describe the Customer role inconsistently:

- SRS §3 ("Target Users") lists only internal staff — Sales, Warehouse Staff, Delivery Staff, Customer Service, Operator/Manager — as actors who directly operate the system. Customer appears separately, under §4 ("Other Stakeholders"), implying Customer does not log in; Sales records orders on the customer's behalf (SRS §3.1: "Receive or record order requests... Enter customer and order information").
- `01-project-definition.md` §4.1 and §5 describe Customer as a first-class, self-service, authenticated role ("Customers use the system to interact with their own orders and browse available products"), on par with Sales/Warehouse/Administrator.
- `01-project-definition.md` is internally inconsistent on this point: §6.3 ("Customer Management") describes Customer purely as data Sales/Admin manage (Create/View/Update/Search), which reads as the SRS model, not the self-service model in §4.1 of the same document.

Separately, the codebase currently has no authentication at all: `src/OrderFlow.Infrastructure/Authentication/` is empty, `Customer` has no password/identity concept, and `CreateOrderCommandHandler` accepts a raw `CustomerId` from the request body with no ownership check — any caller can currently create an order for any customer id.

## Decision

`Customer` (a business record — name, email, phone; owns orders) and `User` (an authentication identity — email, password hash, role) are modeled as **separate entities**, linked by an optional `User.CustomerId` (nullable FK). This allows both source documents to be simultaneously true:

- `CreateCustomer` — Sales/Admin enters a customer record directly (phone/walk-in order). No `User` is created; `User.CustomerId` stays unset for that customer until/unless they self-register.
- `Register` — self-service signup creates a `User` with `Role = Customer`. If the email matches an existing Sales-entered `Customer`, the new `User` is linked to that existing `Customer` (a "claim") instead of creating a duplicate.
- Claiming an existing `Customer` by email match requires OTP email verification before the link is created, to prevent an attacker who merely knows someone else's email from taking over that customer's order history.
- Email delivery for v1 is a stub/log-based `IEmailSender` (no real SMTP/provider integration), consistent with the project's existing stance of not integrating real external payment or delivery providers.

`Order.CustomerId` is unaffected — orders remain owned by the business `Customer` record, not by the login identity. Authorization for customer-facing endpoints compares `currentUser.CustomerId` (when `Role == Customer`) against `order.CustomerId`; Sales/Warehouse/Administrator roles bypass this check per their broader permissions.

## Technical Design

### Entities

**`User`** (new Domain entity)

| Field | Notes |
|---|---|
| `Email` | unique, required |
| `PasswordHash` | required |
| `Role` | enum: `Customer, Sales, Warehouse, Administrator` |
| `CustomerId` | nullable FK to `Customer`; set only for `Role = Customer` accounts, and only once linked/claimed |
| `IsEmailVerified` | bool, default `false` |

**`EmailVerificationOtp`** (new Domain entity, not fields on `User`)

Kept as its own table rather than fields on `User` so a resend generates a fresh row instead of overwriting history, and so a consumed/expired OTP doesn't need to be manually cleared off `User`.

| Field | Notes |
|---|---|
| `UserId` | FK to `User` |
| `Code` | 6-digit string |
| `ExpiresAt` | `CreatedAt + 10 minutes` (implementation default; not a business decision, just a reasonable value — flag if you want a different window) |
| `ConsumedAt` | nullable; set once used, so a consumed code can't be replayed |

### Flow

- **`CreateCustomer`** (Sales/Admin): creates a `Customer` row only. No `User` row. Unchanged from what `ICustomerRepository` already assumes.
- **`Register`** (self-service):
  1. If no existing `Customer` has this email → create `Customer` **and** `User` (`IsEmailVerified = false`) together, generate an `EmailVerificationOtp`, call `IEmailSender` (stub). **Assumption**: a brand-new registration does not block on OTP to finish signing up — the account exists immediately but unverified. OTP is only a hard gate for the *claim* path below, because that's the actual attack this ADR is defending against (taking over an existing customer's order history), not general email hygiene. Flag if you'd rather require verification uniformly for every registration.
  2. If an existing `Customer` has this email (Sales-entered, no `User` yet) → create the `User` row **unlinked** (`CustomerId = null`, `IsEmailVerified = false`), generate an OTP, send it. `CustomerId` is only set once `ConfirmEmailVerificationOtp` succeeds — this is the "claim".
- **`ConfirmEmailVerificationOtp`**: given `UserId` + `Code`, find the newest non-consumed, non-expired `EmailVerificationOtp` row for that user; if it matches, set `ConsumedAt`, set `User.IsEmailVerified = true`, and if this was a claim (matching `Customer` existed), set `User.CustomerId` now.
- **`Login`**: unaffected by verification status for now — whether an unverified `User` can log in at all is a separate, smaller decision not blocking this ADR; default assumption is yes (login works, but the claim/link only completes after verification).

## Alternatives Considered

### Option 1 — Customer never logs in (SRS model only)

`User` (staff only) is entirely unrelated to `Customer`. Simplest, matches SRS literally.

Rejected: cannot deliver `01-project-definition.md` §4.1's explicit self-service Customer responsibilities (view own orders, cancel eligible orders) — would silently shrink the MVP scope that was deliberately expanded to include this.

### Option 2 — Merge Customer and User into one entity

A single `User`-like entity carries both business-record fields and authentication fields, with `Role` distinguishing Customer/Sales/Warehouse/Administrator. `Order.CustomerId` becomes `Order.UserId`.

Rejected: forces authentication fields (password hash, etc.) onto every Sales-entered walk-in customer who may never log in, and couples the `Order`-owning business concept to an authentication concern — contradicting SDS §6 ("Domain must remain independent of... ASP.NET Core Identity implementation").

## Consequences

### Positive

- Reconciles SRS and `01-project-definition.md` instead of picking one and contradicting the other.
- Keeps `Customer` free of authentication concerns; `User` carries them.
- Supports both real business flows: phone/walk-in orders (no login ever) and self-service registration.

### Negative

- More moving parts than either alternative: two entities, a nullable link, two creation flows, and an OTP-based claim flow.
- Introduces a new capability that did not exist before — email sending (even if stubbed for v1) — and a new pending-verification state.
- Expands the use-case roadmap by roughly 4 items: `Register`, `ConfirmEmailVerificationOtp`, an `IEmailSender` abstraction plus a stub implementation, and (implicitly) an OTP resend path.
- No account-recovery flow (forgotten password, lost OTP) is defined yet; deferred as a known v1 gap.

## Related Documents

- `docs/01-project-definition.md` §4.1, §4.4, §5, §6.3, §15
- `docs/02-SRS.md` §3, §4
- `docs/03-SDS.md` §6, §15
- `src/OrderFlow.Domain/Entities/Customer.cs`
