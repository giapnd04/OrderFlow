# ADR-007: Concurrency Strategy for Stock Reservation

## Status

Measurements and analysis: Accepted (31/10/2026, OF-04 part 2).
Change to CreateOrder: Accepted and implemented (31/10/2026, OF-04 part 3).

## Context

CreateOrder reserves stock by loading the product, calling `Product.Reserve(quantity)` and saving. Product carries a rowversion, so a write based on a stale read updates 0 rows and fails with `DbUpdateConcurrencyException`, which the API maps to 409.

Day 4 (`CreateOrderConcurrencyTests`) proved this never oversells the last unit. Rowversion answers "can we oversell?", but not "do we turn buyers away while stock is still there?". To answer that and choose a strategy on evidence, four ways of doing the same reserve step were implemented in the test project (`tests/OrderFlow.IntegrationTests/Orders/ReservationStrategies.cs`) and raced against each other (`ReservationStrategyComparisonTests`). Production code was not changed.

The four strategies:

- RowVersion: today's production behaviour. Read, check in the domain, write. Rowversion rejects a stale write.
- RowVersionRetry3: the same, but a lost rowversion race re-reads the row on a fresh DbContext and tries again, up to 3 retries. A real "insufficient stock" is never retried.
- ConditionalUpdate: one statement, `UPDATE products SET reserved_quantity = reserved_quantity + @qty WHERE id = @id AND stock_quantity - reserved_quantity >= @qty`. 0 rows updated means no stock.
- UpdLock: inside a transaction, `SELECT ... FROM products WITH (UPDLOCK, ROWLOCK) WHERE id = @id`, then `Product.Reserve` and save. Other buyers wait for the row lock.

## Measurements

Environment: SQL Server LocalDB on the developer machine, .NET 9, EF Core 9. Each scenario releases N concurrent attempts at once, each with its own DbContext and transaction. One warm-up round runs before the timed round. The whole set was run 20 times; all 20 runs passed. Times are the median of the 20 runs for all N attempts to finish.

Scenario A, stock 1, N = 10. Every strategy sold exactly 1 unit in all 20 runs.

- RowVersion: 4 ms.
- RowVersionRetry3: 6 ms.
- ConditionalUpdate: 4.5 ms.
- UpdLock: 6 ms.

Scenario B, stock 1, N = 50. Every strategy sold exactly 1 unit in all 20 runs.

- RowVersion: 22 ms.
- RowVersionRetry3: 33.5 ms.
- ConditionalUpdate: 26 ms.
- UpdLock: 21 ms.

Scenario C, stock 10, N = 50. This is where the strategies differ.

- RowVersion sold 1 to 3 units, average 1.4. About 48 buyers got 409 although 8 or 9 units were still in stock. Median 24 ms.
- RowVersionRetry3 sold 7 to 9 units, average 8.0. It still never sold all 10. Median 125 ms, about 4 to 5 times slower than the others.
- ConditionalUpdate sold exactly 10 in all 20 runs. Median 29 ms.
- UpdLock sold exactly 10 in all 20 runs. Median 31 ms.

No strategy ever oversold, and no run produced a deadlock or timeout.

Code size of the reserve step, as written in the experiment:

- RowVersion: 11 lines. Uses the domain method `Product.Reserve`. No raw SQL.
- RowVersionRetry3: 20 lines. Same as RowVersion, plus a loop and a retry limit.
- ConditionalUpdate: 24 lines. The stock rule is written in SQL, so `Product.Reserve` is bypassed. Needs a second query for the price and an explicit transaction.
- UpdLock: 17 lines. One raw SQL query for the locking read, then the normal domain method. Needs an explicit transaction.

## Analysis

Under contention, optimistic concurrency only works well when conflicts are rare. With 50 buyers for 10 units, almost everyone reads the same row version, one writer wins, and every other writer fails. The conflict is false: they failed because the row changed, not because stock ran out.

Retry treats the symptom. Each round of retries is another race with the same shape, so 3 retries still left 2 units unsold on average, and the extra round trips made it the slowest strategy by far. More retries would sell more, but cost even more time and load under exactly the traffic that hurts most.

ConditionalUpdate and UpdLock remove the race instead. Both make concurrent buyers wait for the row lock and then decide on the current value, so the number of successful reservations is always min(stock, N). Their times were within a few milliseconds of RowVersion.

Between the two:

- ConditionalUpdate holds the lock for the shortest time and is the classic single-statement answer. But the invariant "available >= quantity" then lives in SQL as well as in `Product.Reserve`, and the tracked Product entity is not used for the reservation.
- UpdLock keeps `Product.Reserve` as the single place where the rule lives, and fits the existing `ITransactionRunner` (ADR-006). It holds the lock until commit, which is slightly longer.

## Decision

Keep rowversion on Product and Order. It stays the safety net for every other write: product update, restock, cancel, ship.

Do not add retry to CreateOrder. The measurements show retry is slower and still turns buyers away.

Change CreateOrder's reservation to UpdLock (implemented, see Implementation below).

- Load the ordered products with `WITH (UPDLOCK, ROWLOCK)` inside `ITransactionRunner`.
- Lock rows in ascending product id order, so two multi-product orders cannot lock the same rows in opposite order and deadlock.
- Keep calling `Product.Reserve` for the rule.
- Rowversion still applies on save, but with the lock held it should no longer fire for this path.

UpdLock is chosen over ConditionalUpdate because it measured the same in correctness and close in time, while keeping the business rule in the domain model. If profiling later shows lock time on hot products is a problem, ConditionalUpdate is the fallback.

## Implementation

- `IProductRepository.GetByIdsWithUpdateLockAsync` (new). `ProductRepository` runs `SELECT * FROM products WITH (UPDLOCK, ROWLOCK) WHERE id IN (...)` with one SQL parameter per id. The ids are de-duplicated and sorted ascending. The hint sits on the statement that filters by id; composing LINQ on top of `FromSql` would wrap it in a subquery and lock every row scanned.
- `CreateOrderCommandHandler` takes `ITransactionRunner`. Input validation stays outside; the customer check, the locking read, the product checks, `Product.Reserve`, `Order.Create` and the insert run inside one transaction (`ReserveAndCreateAsync`).
- The SQL log of `CreateOrderRollbackTests` shows the sequence: Began transaction, `SELECT * FROM products WITH (UPDLOCK, ROWLOCK) WHERE id IN (@p0)`, `INSERT INTO [orders]`, `UPDATE [products]`, then the injected failure and Disposing transaction without commit.

Results after the change, end to end through the HTTP API (`CreateOrderConcurrencyTests`), 20 runs, all 20 passed. The results were identical in every run:

- Stock 1, N = 10: 1 order created, 9 x 409 "Insufficient stock", 0 x "Concurrent modification".
- Stock 1, N = 50: 1 created, 49 x "Insufficient stock", 0 x "Concurrent modification".
- Stock 10, N = 50: 10 created, 40 x "Insufficient stock", 0 x "Concurrent modification". Before the change, the same reservation logic sold about 1.4 units in this scenario.

The test asserts that "Concurrent modification" stays at 0: buyers now queue on the row lock, so rowversion no longer has to reject anyone on this path.

## Consequences

### Positive

- Every claim above is backed by a repeatable test on SQL Server, not by assumption.
- A buyer now gets 409 "Insufficient stock" only when stock is really gone.

### Negative

- The change adds a raw SQL query to the product repository and requires CreateOrder to run in an explicit transaction.
- Locks are held for the length of the CreateOrder transaction. Hot products serialise their buyers by design.
- The numbers come from LocalDB on one machine. Absolute times will differ on a real server; the relative picture (Scenario C) is the important part.
- Read Committed isolation is unchanged. Isolation levels and SNAPSHOT are covered in OF-15.

## Related Documents

- ADR-004 Inventory Reservation
- ADR-006 Transaction Boundary
- `docs/06-testing.md`, sections 8 and 9, gap G-04
- Microsoft Learn, EF Core "Handling Concurrency Conflicts"; SQL Server "Transaction locking and row versioning guide"
