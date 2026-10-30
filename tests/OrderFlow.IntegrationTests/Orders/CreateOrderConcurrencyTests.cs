using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Domain.Enums;
using OrderFlow.IntegrationTests.Fixtures;
using OrderFlow.IntegrationTests.Security;
using Xunit.Abstractions;

namespace OrderFlow.IntegrationTests.Orders;

// OF-04 / G-04: N buyers race for the last unit. Rowversion on products must let exactly one
// reservation through; everyone else gets 409, and stock is never oversold.
[Collection(SqlServerCollection.Name)]
public sealed class CreateOrderConcurrencyTests : IAsyncLifetime
{
    private readonly SqlServerFixture _sql;
    private readonly ITestOutputHelper _output;

    public CreateOrderConcurrencyTests(SqlServerFixture sql, ITestOutputHelper output)
    {
        _sql = sql;
        _output = output;
    }

    public Task InitializeAsync() => _sql.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    public async Task ConcurrentCreateOrder_LastUnit_ExactlyOneSucceeds(int requests)
    {
        var (customerId, productId) = await TestData.SeedCustomerAndProductAsync(_sql, stockQuantity: 1);

        await using var factory = new SqlApiFactory(_sql.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.For(UserRole.Sales));

        // Hold every request at the same starting line so they overlap in the database.
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var body = new { customerId, items = new[] { new { productId, quantity = 1 } } };

        var calls = Enumerable.Range(0, requests).Select(async _ =>
        {
            await start.Task;
            using var response = await client.PostAsJsonAsync("/api/orders", body);
            return (response.StatusCode, Body: await response.Content.ReadAsStringAsync());
        }).ToList();

        start.SetResult();
        var results = await Task.WhenAll(calls);

        var unexpected = results.Where(r => r.StatusCode is not (HttpStatusCode.Created or HttpStatusCode.Conflict)).ToList();
        Assert.True(unexpected.Count == 0,
            $"{unexpected.Count} unexpected response(s), first: {unexpected.FirstOrDefault().StatusCode} {unexpected.FirstOrDefault().Body}");

        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Created));

        var conflictTitles = results
            .Where(r => r.StatusCode == HttpStatusCode.Conflict)
            .Select(r => JsonDocument.Parse(r.Body).RootElement.GetProperty("title").GetString())
            .ToList();

        _output.WriteLine(
            $"RESULT N={requests} created=1 " +
            $"concurrentModification={conflictTitles.Count(t => t == "Concurrent modification")} " +
            $"insufficientStock={conflictTitles.Count(t => t == "Insufficient stock")}");

        await using var db = _sql.CreateDbContext();
        var product = await db.Products.SingleAsync(p => p.Id == productId);

        Assert.Equal(1, await db.Orders.CountAsync());
        Assert.Equal(1, product.ReservedQuantity);
        Assert.True(product.ReservedQuantity <= product.StockQuantity);
    }
}
