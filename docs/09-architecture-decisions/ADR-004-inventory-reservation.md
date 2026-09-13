# ADR-004: Inventory Reservation Model

## Status

Accepted

## Context

The original schema-v1 review (see project memory, `orderflow-schema-v1`) deliberately chose no reservation mechanism: stock is decremented only at payment success, and the resulting oversell gap between order creation and payment was accepted as a known v1 limitation.

`01-project-definition.md` §6.5 describes a different model explicitly: `Available Quantity = Stock Quantity - Reserved Quantity`, with required operations to "Check inventory, Reserve stock, Release reserved stock, Deduct stock after fulfillment" and to "Prevent orders from exceeding available inventory" — at order-creation time, not only at payment time. `02-SRS.md` §35 lists the exact inventory reservation/allocation model as not yet finalized.

Under the current implementation (no reservation), two customers can both successfully create an order for the last unit of a product; only one can actually complete payment, and the other discovers the failure only at payment time, after having gone through the full checkout flow.

## Decision

Add a `ReservedQuantity` field directly on `Product` (not a separate `Inventory` entity/table):

- `CreateOrder` calls `Product.Reserve(quantity)` per line, which checks `StockQuantity - ReservedQuantity >= quantity` and throws `InsufficientStockException` otherwise, then increments `ReservedQuantity`.
- `CancelOrder`, when cancelling from the pre-payment state, calls `Product.ReleaseReservation(quantity)` (decrements `ReservedQuantity`; `StockQuantity` is untouched, since nothing was ever removed from it).
- `ProcessPayment`, on success, calls `Product.FulfillReservation(quantity)` (replacing today's `DecreaseStock`): decrements both `StockQuantity` and `ReservedQuantity` together, converting the reservation into a real deduction.
- `CancelOrder`, when cancelling from `Confirmed` (i.e., after payment — permitted per ADR-003), calls a new `Product.Restock(quantity)`: increments `StockQuantity` directly, since no reservation remains at that point to release. This addresses only the stock side of a post-payment cancellation; no refund/money mechanism exists (refund remains out of v1 scope, as originally decided).
- Reservations have **no expiry** in v1: an order created but never paid holds its reservation indefinitely until explicitly cancelled. This is an accepted, documented limitation — the same posture as the oversell gap it replaces — not an oversight.

## Technical Design

### `Product` — new methods (in addition to existing `Create`)

| Method | Precondition | Effect |
|---|---|---|
| `Reserve(int quantity)` | `quantity > 0`; `StockQuantity - ReservedQuantity >= quantity`, else `InsufficientStockException` | `ReservedQuantity += quantity` |
| `ReleaseReservation(int quantity)` | `quantity > 0`; `ReservedQuantity >= quantity` (invariant, should never fail if call sites are correct) | `ReservedQuantity -= quantity` |
| `FulfillReservation(int quantity)` | same as `ReleaseReservation` | `StockQuantity -= quantity; ReservedQuantity -= quantity` (replaces today's `DecreaseStock`) |
| `Restock(int quantity)` | `quantity > 0` | `StockQuantity += quantity` (no invariant to violate — stock only grows) |

Invariant that must hold after every operation: `0 <= ReservedQuantity <= StockQuantity`.

### Exact call sites (reading current handler code, not a generic description)

- **`CreateOrderCommandHandler`**: it already loads `productsById` via `_products.GetByIdsAsync(...)` (tracked, no `AsNoTracking`) to snapshot price and check `ProductStatus.Active`. Add the `Reserve` call inside the same `requestedQuantities` loop, before/alongside building each `OrderItem` — e.g. right where `productsById[kvp.Key].Price` is read, also call `productsById[kvp.Key].Reserve(kvp.Value)`. No new repository method needed; `IProductRepository` is already injected here.
- **`CancelOrderCommandHandler`**: currently injects only `IOrderRepository`. Needs `IProductRepository` added. Critical ordering: **read `order.Status` before calling `order.Cancel()`** — after `Cancel()` runs, `Status` becomes `Cancelled` and the information "which state it cancelled from" is lost. So: capture `wasPendingPayment = order.Status == OrderStatus.PendingPayment` first, then call `order.Cancel()`, then load the order's products (same `GetByIdsAsync` pattern) and, per item, call `ReleaseReservation` if `wasPendingPayment` was true, or `Restock` otherwise (it can only have been `Confirmed`, per ADR-003's transition table — `Cancel()` itself already rejects every other state).
- **`ProcessPaymentCommandHandler`**: already calls `product.DecreaseStock(item.Quantity)` in a loop over `order.Items` after `succeededAmount >= order.TotalAmount`. Rename this call to `FulfillReservation` (same call site, same loop — no structural change).

### Database

Add `reserved_quantity` (`int`, `NOT NULL`, default `0`) to `products`, plus CHECK constraints `reserved_quantity >= 0` and `reserved_quantity <= stock_quantity` (mirrors the `Product` invariant at the DB level, same defense-in-depth pattern already used for `order_items`/`payment_attempts`).

## Alternatives Considered

### Option A — Keep `StockQuantity` only, no reservation

Zero change from the current implementation.

Rejected: does not satisfy `01-project-definition.md` §6.5's explicit requirement to prevent orders from exceeding available stock at creation time, and leaves the "two customers, one unit" scenario resolved only at payment time, after checkout has already completed once from the customer's perspective.

### Option C — Separate `Inventory` entity/table

Model stock (`StockQuantity` + `ReservedQuantity`) as its own aggregate in a new `inventory` table, 1:1 with `products`, matching the original pre-review schema draft.

Rejected as premature for v1, per SDS's own stated principle ("do not introduce an abstraction or infrastructure component until a concrete requirement justifies it"): no concrete v1 requirement (e.g., multi-warehouse, stock independent of a single product) demands a separate aggregate. It would also reverse the `Product` encapsulation work already implemented and reviewed for this project.

## Consequences

### Positive

- Satisfies `01-project-definition.md`'s explicit reservation requirement with the smallest schema/domain change: one new column, three new `Product` domain methods.
- Reuses the multi-aggregate-write pattern already established by `ProcessPayment` (same `DbContext`, one implicit transaction via `SaveChanges`).

### Negative

- `CreateOrder` and `CancelOrder` — two of the three already-implemented, already-tested handlers — become multi-aggregate writes and now require `IProductRepository` where they did not before.
- The no-expiry decision is an accepted gap: reservations can accumulate indefinitely from abandoned, never-paid orders. No cleanup job or expiry policy is planned for v1.
- The `Confirmed`-cancel restock path (see ADR-003) only reverses the stock effect of a payment, not the money — a real refund flow remains undesigned and out of v1 scope.

## Related Documents

- `docs/01-project-definition.md` §6.5
- `docs/02-SRS.md` §30, §35
- `src/OrderFlow.Domain/Entities/Product.cs`
- `docs/09-architecture-decisions/ADR-003-order-lifecycle.md`
