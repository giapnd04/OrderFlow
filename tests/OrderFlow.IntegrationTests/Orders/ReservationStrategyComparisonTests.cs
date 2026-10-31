using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using OrderFlow.IntegrationTests.Fixtures;
using Xunit.Abstractions;

namespace OrderFlow.IntegrationTests.Orders;

// OF-04 part 2: the same race run against each reservation strategy. Every strategy must
// never oversell; the measurements (conflicts, elapsed ms) feed ADR-007.
[Collection(SqlServerCollection.Name)]
public sealed class ReservationStrategyComparisonTests : IAsyncLifetime
{
    private readonly SqlServerFixture _sql;
    private readonly ITestOutputHelper _output;

    public ReservationStrategyComparisonTests(SqlServerFixture sql, ITestOutputHelper output)
    {
        _sql = sql;
        _output = output;
    }

    public Task InitializeAsync() => _sql.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    public static TheoryData<ReservationStrategy, int, int> Scenarios()
    {
        var data = new TheoryData<ReservationStrategy, int, int>();

        foreach (var strategy in Enum.GetValues<ReservationStrategy>())
        {
            data.Add(strategy, 1, 10);
            data.Add(strategy, 1, 50);
            data.Add(strategy, 10, 50);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public async Task ConcurrentReservations_NeverOversell(ReservationStrategy strategy, int stock, int requests)
    {
        // Warm-up round (JIT, connection pool) so the timed round compares strategies, not cold starts.
        await RaceAsync(strategy, requests, await TestData.SeedCustomerAndProductAsync(_sql, stock));
        await _sql.ResetDatabaseAsync();

        var (customerId, productId) = await TestData.SeedCustomerAndProductAsync(_sql, stock);

        var stopwatch = Stopwatch.StartNew();
        var outcomes = await RaceAsync(strategy, requests, (customerId, productId));
        stopwatch.Stop();

        var reserved = outcomes.Count(o => o == ReservationOutcome.Reserved);

        _output.WriteLine(
            $"RESULT strategy={strategy} stock={stock} N={requests} reserved={reserved} " +
            $"conflicts={requests - reserved} ms={stopwatch.ElapsedMilliseconds}");

        await using var db = _sql.CreateDbContext();
        var product = await db.Products.SingleAsync(p => p.Id == productId);

        Assert.InRange(reserved, 1, stock);
        Assert.Equal(reserved, await db.Orders.CountAsync());
        Assert.Equal(reserved, product.ReservedQuantity);
        Assert.True(product.ReservedQuantity <= product.StockQuantity);

        if (strategy is ReservationStrategy.ConditionalUpdate or ReservationStrategy.UpdLock)
        {
            // Both serialise on the row lock, so nobody is turned away while stock remains.
            Assert.Equal(Math.Min(stock, requests), reserved);
        }
    }

    // Releases every attempt at once so they overlap in the database.
    private async Task<ReservationOutcome[]> RaceAsync(
        ReservationStrategy strategy, int requests, (int CustomerId, int ProductId) ids)
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = Enumerable.Range(0, requests).Select(async _ =>
        {
            await start.Task;
            return await ReservationStrategies.ReserveAsync(strategy, _sql, ids.CustomerId, ids.ProductId, quantity: 1);
        }).ToList();

        start.SetResult();
        return await Task.WhenAll(attempts);
    }
}
