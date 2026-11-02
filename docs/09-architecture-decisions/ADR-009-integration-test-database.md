# ADR-009: Database for Integration Tests

## Status

Accepted (02/11/2026, OF-02). Written after the fixture was built on 28/10/2026.

## Context

Until OF-02, no test touched a real database. Unit tests use fake repositories, and the security integration tests use a connection string that cannot connect. Transactions, CHECK constraints, unique indexes, rowversion and lock hints therefore had no evidence behind them.

Options for a real database in tests have different costs. The development machine had LocalDB but no Docker, and the CI planned in OF-11 will run on Linux, where LocalDB does not exist.

## Decision

`SqlServerFixture` supports two sources, with no code change between them:

- If the environment variable `ORDERFLOW_TEST_CONNECTION` is set, the tests use that database. On the development machine this is LocalDB.
- Otherwise the fixture starts a disposable SQL Server container with Testcontainers (`mcr.microsoft.com/mssql/server:2022-latest`). This is the path for CI and for machines with Docker.

The schema is created with `Database.MigrateAsync()`, so the tests run against the same migrations as production. `EnsureCreated` is not used because it would skip them.

Data is reset before each test with Respawn. It deletes rows in foreign-key order and keeps the schema and `__EFMigrationsHistory`.

Because a reset deletes everything, the fixture refuses a configured database whose name does not contain "Test". The development database `OrderFlow` can never be wiped by accident.

All database test classes share one fixture through `[Collection("SqlServer")]`. xUnit runs them one after another, so they cannot see each other's data.

## Alternatives Considered

### Option 1: EF Core InMemory or SQLite

- Advantage: fast, no setup.
- Disadvantage: InMemory has no transactions, no CHECK constraints and no rowversion behaviour. SQLite has different locking and no UPDLOCK. The bugs this project needed to find would stay invisible.

### Option 2: Testcontainers only

- Advantage: one path, and identical everywhere.
- Disadvantage: tests could not run at all on the development machine until Docker was installed.

### Option 3: LocalDB only

- Advantage: simplest.
- Disadvantage: Windows only, so it cannot run in Linux CI.

### Option 4: Reset by rolling back a transaction around each test

- Advantage: very fast, no extra package.
- Disadvantage: concurrency tests use several connections and need committed data, which a wrapping transaction would hide. The tests that prove rollback behaviour also need real commits.

## Consequences

### Positive

- Tests exercise the real migrations, constraints, locks and transactions.
- One command runs everything, on LocalDB or in a container.

### Negative

- The Testcontainers path has not run for real yet, because this machine has no Docker. So far only its error message for missing Docker has been checked. OF-11 must verify it in CI.
- Without the environment variable and without Docker, every database test fails. The error message explains both ways to fix it; the other tests still run.
- LocalDB and the Linux container are different SQL Server editions. Timing numbers from LocalDB (ADR-007) are not portable.

## Related Documents

- `docs/06-testing.md`, sections 6 and 12, gaps G-03 and G-09
- Microsoft Learn, EF Core "Testing against your production database system"
