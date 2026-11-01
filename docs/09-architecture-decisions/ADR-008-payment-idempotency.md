# ADR-008: Payment Idempotency and Over-Payment Guard

## Status

Accepted (01/11/2026, OF-05)

## Context

`POST /api/orders/{id}/payments` records what a payment provider reports. Providers retry callbacks, and two different payments can arrive for the same order at the same moment. Before this decision, integration tests on SQL Server (`PaymentIdempotencyTests`) showed:

- A Succeeded payment could be recorded without a provider transaction id. The unique index on (provider, provider_transaction_id) is filtered to non-NULL ids, so such a payment could be recorded twice.
- Sending the same payment again returned 409 "Invalid order state" (the first one had already paid the order), or 409 "Duplicate value" when the two copies raced. The provider cannot tell that apart from a real failure.
- Two partial payments of 15 sent at the same time for an order of 20 were both accepted. The order was marked Paid with 30 recorded.

## Decision

1. A Succeeded payment must carry `ProviderTransactionId`. The handler rejects it with 400, and the CHECK constraint `ck_payment_attempts_succeeded_requires_provider_tx` (`[status] <> N'Succeeded' OR [provider_transaction_id] IS NOT NULL`) enforces it in the database. Migration `RequireProviderTransactionId` first gives old rows without an id the placeholder `legacy-<id>`, so the constraint can be applied to existing data.

2. Replay semantics, keyed on (provider, provider transaction id):
   - Same order, amount and status as the recorded attempt: nothing is written, and the original result (same payment attempt id, current order status) is returned with 200.
   - Same key with any different detail: 409, nothing changes.

3. Payments for one order run one at a time. `OrderRepository.GetByIdForUpdateAsync` reads the order with `WITH (UPDLOCK, ROWLOCK)`. ProcessPayment already runs inside `ITransactionRunner` (ADR-006), so the lock lasts until commit. The order is locked before the replay lookup, so an identical twin request waits, then finds the first attempt and is answered as a replay.

4. Over-payment guard: a Succeeded payment is refused with 409 when `already paid + amount > order total`. Because of the order lock, "already paid" cannot change between the check and the insert.

The filtered unique index stays as the last line of defence. If application code ever skips the lookup, the database still refuses a second row with the same provider id.

## Alternatives Considered

### Option 1: Rely on the unique index only

- Advantage: no extra code.
- Disadvantage: a retried callback gets 409 instead of the original answer, and the index cannot stop two different ids from over-paying an order.

### Option 2: A separate idempotency-key table for every write endpoint

- Advantage: generic, works for any endpoint.
- Disadvantage: more moving parts than this project needs. The provider transaction id is already a natural idempotency key for payments. A generic Idempotency-Key header for CreateOrder is tracked separately as OF-16.

## Consequences

### Positive

- Every acceptance criterion of OF-05 is covered by a test on SQL Server. The concurrent tests passed 20 out of 20 runs.
- A provider can retry safely: it always gets the same answer for the same payment.

### Negative

- Payments for the same order are serialised. This is cheap here, since one order rarely receives many payments at once, but it is a deliberate bottleneck.
- `GetByIdForUpdateAsync` now takes an update lock. Callers outside a transaction (Cancel, Confirm, Ship, Deliver) only hold it for the single SELECT, so their behaviour does not change. A future caller that wraps it in a long transaction would hold the order lock for that long.
- Old Succeeded rows without an id now carry a `legacy-` placeholder instead of NULL.

## Related Documents

- ADR-006 Transaction Boundary
- ADR-007 Concurrency Strategy for Stock Reservation
- `docs/06-testing.md`, section 11, gap G-02
