# ADR-006: Transaction Boundary for Multi-Write Use Cases

## Status

Accepted (29/10/2026, OF-03)

## Context

Repositories in OrderFlow persist on their own: `AddAsync` and `UpdateAsync` each call `SaveChangesAsync`. One SaveChanges is atomic, because EF Core wraps it in an implicit transaction. Two SaveChanges calls are not.

`ProcessPaymentCommandHandler` made two:

1. `PaymentRepository.AddAsync` inserted the payment attempt and committed.
2. `OrderRepository.UpdateAsync` saved the fulfilled products and the order marked as Paid.

If step 2 failed (deadlock victim, timeout, process killed), the database kept a Succeeded payment for an order still in PendingPayment, with its stock still only reserved. An integration test that fails the second SaveChanges (`ProcessPaymentAtomicityTests`) reproduced this on SQL Server: 1 payment row remained while the order stayed PendingPayment.

The same handler also needs the first write to be visible to its own next query: `GetSucceededAmountAsync` sums payments from the database, including the one just inserted.

## Decision

A use case that writes through more than one SaveChanges runs inside `ITransactionRunner` (implemented by `EfTransactionRunner`). The runner begins one database transaction on the scoped `OrderFlowDbContext`, runs the use case, and commits only if it finishes. Every repository shares that context, so each SaveChanges joins the same transaction. An exception disposes the transaction without committing, which rolls back every write.

`ProcessPaymentCommandHandler` now validates its input outside the transaction, then runs everything from loading the order to the final update inside `ExecuteAsync`. Register and ConfirmEmailOtp already followed this rule.

A use case that writes through exactly one SaveChanges does not need the runner. CreateOrder and CancelOrder stay as they are. `CreateOrderRollbackTests` proves this: it fails the `INSERT INTO [order_items]` statement after `INSERT INTO [orders]` and the product reservation update have already run, and the database ends with no order and no reservation.

The rule in one line: one SaveChanges means the implicit transaction is enough; two or more means the use case must be wrapped in ITransactionRunner.

## Alternatives Considered

### Option 1: Collapse ProcessPayment into a single SaveChanges

Make repositories only track changes and let the handler save once at the end (a classic Unit of Work).

- Advantage: no explicit transaction, shorter lock time.
- Disadvantage: changes every repository and every handler. The payment sum would also have to be computed in memory instead of by the database, because the new payment would not be saved yet when the sum runs.

### Option 2: TransactionScope

- Advantage: works across several DbContexts or connections.
- Disadvantage: OrderFlow has one DbContext, so there is nothing to coordinate. Async code needs `TransactionScopeAsyncFlowOption`, and a mistake can escalate to a distributed transaction. The EF Core documentation recommends `BeginTransaction` when a single context is enough.

## Consequences

### Positive

- A payment can no longer exist without the order and stock changes it caused, and the reverse is also true.
- The rule is testable: fault-injection interceptors in `tests/OrderFlow.IntegrationTests/Fixtures/FaultInjection.cs` make any SaveChanges or SQL statement fail on demand.

### Negative

- The transaction stays open across several round trips, so row locks on the order, payment and products are held longer than with two separate commits.
- The default isolation level (Read Committed) is still in use. Two concurrent payments on the same order are not prevented by this ADR. That belongs to OF-04 (rowversion under concurrency) and OF-05 (payment idempotency). Isolation level choices are covered in OF-15.
- `EfTransactionRunner` does not use an execution strategy. If retry-on-failure (`EnableRetryOnFailure`) is turned on later, the runner must be changed to run inside `Database.CreateExecutionStrategy()`.

## Related Documents

- `docs/06-testing.md`, gap G-01
- ADR-004 Inventory Reservation (FulfillReservation happens inside this transaction)
- Microsoft Learn, EF Core "Transactions"
