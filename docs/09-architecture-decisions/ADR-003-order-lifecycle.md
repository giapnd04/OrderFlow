# ADR-003: Order Lifecycle — Deliberate Workflow Projection of Payment Status

## Status

Accepted

## Context

Three inconsistent Order lifecycles exist across the project:

| Source | Lifecycle |
|---|---|
| `01-project-definition.md` §7-8 | `Pending → Confirmed → Processing → Shipped → Delivered` (+ `Cancelled`), with an explicit transition table |
| `02-SRS.md` §9 | `Draft → Confirmed → Preparing → Shipped → Delivered → Completed` (+ `On Hold`/`Cancelled`/`Failed`/`Change Requested`) |
| `OrderStatus.cs` (implemented, migrated) | `PendingPayment → Paid → Processing → Completed` (+ `Cancelled`) |

`02-SRS.md` §21 states explicitly: "Order status and Payment status must not be treated as the same lifecycle." The implemented enum violates this directly — `PendingPayment` and `Paid` are payment-flavored values living inside the Order's own status field. This was not an oversight: an earlier database-design review (see project memory, `orderflow-schema-v1`) deliberately chose `payment_attempt` as the sole source of truth for "is this order paid", with `Order.Status = Paid` kept only as a convenience projection ("on disagreement, SUM wins") — but this tradeoff was never reconciled against the SRS wording, and `02-SRS.md` §35 itself lists "exact order states" as not yet finalized.

Two further gaps were found in the current implementation:

- `Processing` and `Completed` exist in the enum and the database CHECK constraint but are never reachable by any code path — dead states.
- `Order.Cancel()` only permits cancellation from `PendingPayment`. Both `01-project-definition.md` §11 and SRS's own "Prepaid" policy description (§22: `Payment = Paid → Order can be Confirmed`) imply a paid/confirmed order should still be cancellable — the current code makes this permanently impossible once payment succeeds.

## Decision

Keep `Order.Status` as a deliberate, documented workflow projection of payment state rather than fully separating the two lifecycles:

- `payment_attempt` remains the sole source of truth for whether an order is paid; `Order.Status` transitions are a one-way, one-time projection of that fact, not a second ledger.
- Add a `Confirmed` status.
- Remove `Processing` and `Completed` from the enum and the database CHECK constraint until a concrete Fulfillment/Delivery slice defines what triggers them (no dead states in the meantime).
- Extend `Order.Cancel()` to permit cancellation from `Confirmed` in addition to the pre-payment state, closing the gap against `01-project-definition.md` §11.
- `Paid` is a deliberate "locked" state: once payment succeeds, the order cannot be cancelled directly. A Sales action (`ConfirmOrder`, a future use case) must move it to `Confirmed` before it becomes cancellable again. This means the full valid transition set for v1 is:

  ```text
  PendingPayment -> Paid        (ProcessPayment succeeds)
  PendingPayment -> Cancelled   (Cancel)
  Paid           -> Confirmed   (ConfirmOrder — not yet implemented)
  Confirmed      -> Cancelled   (Cancel)
  ```

  `Paid -> Cancelled` is intentionally **not** a valid transition — a human review step (Confirm) is required first. All other transitions not listed above are invalid.

## Alternatives Considered

### Option 1 — Strict SRS separation

Remove `Paid`/`PendingPayment` from `OrderStatus` entirely; Order never encodes payment state. A `ConfirmOrder` use case would need to independently query `PaymentAttempt` and decide whether to confirm, which in turn requires a Payment Policy (Prepaid vs. Cash-on-Delivery) to know when confirmation should happen automatically vs. manually.

Rejected for v1: `02-SRS.md` §22 lists the exact Payment Policy as a separate, still-open TBD item. Adopting this option now would silently drag an unrelated open decision into this one.

### Option 3 — No change, ADR only

Keep the enum and `Cancel()` exactly as they are; only document the tension with SRS §21.

Rejected: leaves the already-scheduled `ConfirmOrder` use case with no status to attach to, and leaves the "cannot cancel a paid order" gap (§11) unresolved.

## Consequences

### Positive

- Minimal rework to the three already-implemented, already-tested handlers (`CreateOrder`, `CancelOrder`, `ProcessPayment`).
- Unblocks the already-planned `ConfirmOrder` use case.
- The tradeoff against SRS §21 is now explicit and justified, not an unnoticed inconsistency.

### Negative

- Still technically diverges from the literal wording of SRS §21; mitigated only by this ADR.
- Allowing cancellation from `Confirmed` (i.e., after payment) means that cancellation now requires a genuine stock **restock**, not merely releasing a reservation — see ADR-004. This reopens a version of the "refund" question that the original schema-v1 review deliberately deferred out of scope; only the stock side is addressed here, not the money side (no refund mechanism exists).

## Related Documents

- `docs/02-SRS.md` §9, §10, §20, §21, §22, §35
- `docs/01-project-definition.md` §7, §8, §11
- `src/OrderFlow.Domain/Entities/Order.cs`
- `src/OrderFlow.Domain/Enums/OrderStatus.cs`
- `docs/09-architecture-decisions/ADR-004-inventory-reservation.md`
