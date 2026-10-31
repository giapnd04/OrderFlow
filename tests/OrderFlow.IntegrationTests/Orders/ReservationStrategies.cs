using Microsoft.EntityFrameworkCore;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Exceptions;
using OrderFlow.Infrastructure.Persistence;
using OrderFlow.IntegrationTests.Fixtures;

namespace OrderFlow.IntegrationTests.Orders;

public enum ReservationStrategy
{
    RowVersion,
    RowVersionRetry3,
    ConditionalUpdate,
    UpdLock,
}

public enum ReservationOutcome
{
    Reserved,
    Conflict,
}

// OF-04 experiment: ways to "reserve one unit and create the order". Only the reserve
// step differs; each attempt runs on its own DbContext and transaction, like one HTTP request.
public static class ReservationStrategies
{
    public const int MaxRetries = 3;

    public static async Task<ReservationOutcome> ReserveAsync(
        ReservationStrategy strategy, SqlServerFixture sql, int customerId, int productId, int quantity)
    {
        if (strategy == ReservationStrategy.RowVersionRetry3)
        {
            return await RowVersionWithRetryAsync(sql, customerId, productId, quantity);
        }

        await using var db = sql.CreateDbContext();

        try
        {
            return strategy switch
            {
                ReservationStrategy.RowVersion => await RowVersionAsync(db, customerId, productId, quantity),
                ReservationStrategy.ConditionalUpdate => await ConditionalUpdateAsync(db, customerId, productId, quantity),
                ReservationStrategy.UpdLock => await UpdLockAsync(db, customerId, productId, quantity),
                _ => throw new ArgumentOutOfRangeException(nameof(strategy)),
            };
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException or InsufficientStockException)
        {
            return ReservationOutcome.Conflict;
        }
    }

    // Production today: read, check in the domain, write; rowversion rejects a stale write.
    private static async Task<ReservationOutcome> RowVersionAsync(
        OrderFlowDbContext db, int customerId, int productId, int quantity)
    {
        var product = await db.Products.SingleAsync(p => p.Id == productId);
        product.Reserve(quantity);

        AddOrder(db, customerId, product.Id, quantity, product.Price);
        await db.SaveChangesAsync();

        return ReservationOutcome.Reserved;
    }

    // Same as RowVersion, but a lost rowversion race re-reads the row on a fresh context and tries
    // again. A real "out of stock" is final and is not retried.
    private static async Task<ReservationOutcome> RowVersionWithRetryAsync(
        SqlServerFixture sql, int customerId, int productId, int quantity)
    {
        for (var attempt = 0; ; attempt++)
        {
            await using var db = sql.CreateDbContext();

            try
            {
                return await RowVersionAsync(db, customerId, productId, quantity);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxRetries)
            {
            }
            catch (Exception ex) when (ex is DbUpdateConcurrencyException or InsufficientStockException)
            {
                return ReservationOutcome.Conflict;
            }
        }
    }

    // Check and write in one statement; SQL Server's row lock serialises the UPDATEs.
    private static async Task<ReservationOutcome> ConditionalUpdateAsync(
        OrderFlowDbContext db, int customerId, int productId, int quantity)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();

        var updated = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE products
            SET reserved_quantity = reserved_quantity + {quantity}, updated_at = SYSUTCDATETIME()
            WHERE id = {productId} AND stock_quantity - reserved_quantity >= {quantity}
            """);

        if (updated == 0)
        {
            return ReservationOutcome.Conflict;
        }

        var price = await db.Products.Where(p => p.Id == productId).Select(p => p.Price).SingleAsync();

        AddOrder(db, customerId, productId, quantity, price);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return ReservationOutcome.Reserved;
    }

    // Pessimistic: take an update lock on the row first, so other buyers queue behind it.
    private static async Task<ReservationOutcome> UpdLockAsync(
        OrderFlowDbContext db, int customerId, int productId, int quantity)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();

        var product = await db.Products
            .FromSqlInterpolated($"SELECT * FROM products WITH (UPDLOCK, ROWLOCK) WHERE id = {productId}")
            .SingleAsync();

        product.Reserve(quantity);

        AddOrder(db, customerId, product.Id, quantity, product.Price);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return ReservationOutcome.Reserved;
    }

    private static void AddOrder(OrderFlowDbContext db, int customerId, int productId, int quantity, decimal price)
        => db.Orders.Add(Order.Create(customerId, [OrderItem.Create(productId, quantity, price)]));
}
