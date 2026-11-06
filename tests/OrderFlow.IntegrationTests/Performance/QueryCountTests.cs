using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Domain.Enums;
using OrderFlow.IntegrationTests.Fixtures;
using OrderFlow.IntegrationTests.Security;

namespace OrderFlow.IntegrationTests.Performance;

// OF-09: how many SQL commands each request costs. A list endpoint must cost the same with 10
// rows as with 100; if it grows with the page size, something is loading per row (N+1).
[Collection(SqlServerCollection.Name)]
public sealed class QueryCountTests : IAsyncLifetime
{
    private const int OrderCount = 25;

    // Customer 1 owns orders 1..20, customer 2 owns 21..25. Orders 1..10 are Paid with a payment.
    private static readonly string SeedSql = $"""
        SET IDENTITY_INSERT customers ON;
        INSERT INTO customers (id, name, email, status) VALUES
            (1, N'First', N'first@example.com', N'Active'),
            (2, N'Second', N'second@example.com', N'Active'),
            (3, N'NoOrders', N'none@example.com', N'Active');
        SET IDENTITY_INSERT customers OFF;

        INSERT INTO products (sku, name, price, stock_quantity, reserved_quantity, status)
        SELECT CONCAT(N'QC-', n), CONCAT(N'Product ', n), 10, 100, 0, N'Active'
        FROM (SELECT TOP ({OrderCount}) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n FROM sys.objects) x;

        SET IDENTITY_INSERT orders ON;
        INSERT INTO orders (id, customer_id, total_amount, status)
        SELECT n, CASE WHEN n <= 20 THEN 1 ELSE 2 END, 10, CASE WHEN n <= 10 THEN N'Paid' ELSE N'PendingPayment' END
        FROM (SELECT TOP ({OrderCount}) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n FROM sys.objects) x;
        SET IDENTITY_INSERT orders OFF;

        INSERT INTO payment_attempts (order_id, amount, status, provider, provider_transaction_id)
        SELECT id, 10, N'Succeeded', N'P', CONCAT(N'QC-', id) FROM orders WHERE id <= 10;
        """;

    private readonly SqlServerFixture _sql;
    private readonly SqlCommandCounter _commands = new();
    private SqlApiFactory? _factory;
    private HttpClient? _client;

    public QueryCountTests(SqlServerFixture sql) => _sql = sql;

    private HttpClient Client => _client ?? throw new InvalidOperationException("Not initialized.");

    public async Task InitializeAsync()
    {
        await _sql.ResetDatabaseAsync();

        await using (var db = _sql.CreateDbContext())
        {
            await db.Database.ExecuteSqlRawAsync(SeedSql);
        }

        _factory = new SqlApiFactory(_sql.ConnectionString, _commands);
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.For(UserRole.Administrator));
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Theory]
    [InlineData("/api/orders")]
    [InlineData("/api/products")]
    [InlineData("/api/customers")]
    [InlineData("/api/payments")]
    public async Task ListEndpoint_CostsTheSameCommandsForAnyPageSize(string path)
    {
        var small = await CommandsForAsync($"{path}?pageSize=10");
        var large = await CommandsForAsync($"{path}?pageSize=100");

        Assert.Equal(small, large);
        Assert.InRange(large, 1, 2);
    }

    [Fact]
    public async Task CustomerOrderStats_UsesOneExistenceCheckAndOneAggregate()
    {
        var commands = await CommandsForAsync("/api/customers/1/order-stats");

        Assert.Equal(2, commands);

        using var response = await Client.GetAsync("/api/customers/1/order-stats");
        var stats = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(20, stats.GetProperty("orderCount").GetInt32());
        Assert.Equal(100m, stats.GetProperty("spentAmount").GetDecimal());
    }

    [Fact]
    public async Task CustomerOrderStats_WithNoOrders_ReturnsZeros()
    {
        using var response = await Client.GetAsync("/api/customers/3/order-stats");
        var stats = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(0, stats.GetProperty("orderCount").GetInt32());
        Assert.Equal(0m, stats.GetProperty("spentAmount").GetDecimal());
        Assert.Equal(JsonValueKind.Null, stats.GetProperty("lastOrderAt").ValueKind);
    }

    [Fact]
    public async Task Cursor_WalksEveryOrderOnce_WithOneCommandPerPage()
    {
        var seen = new List<int>();
        int? afterId = null;

        do
        {
            _commands.Reset();
            var page = await GetCursorPageAsync(Client, afterId, pageSize: 10);

            Assert.Equal(1, _commands.Count);
            seen.AddRange(page.Ids);
            afterId = page.NextAfterId;
        }
        while (afterId is not null);

        Assert.Equal(Enumerable.Range(1, OrderCount), seen);
    }

    [Fact]
    public async Task Cursor_CustomerSeesOnlyOwnOrders()
    {
        using var customer = _factory!.CreateClient();
        customer.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.For(UserRole.Customer, customerId: 2));

        // Asking for customer 1 is ignored: a customer is always scoped to their own record.
        var page = await GetCursorPageAsync(customer, afterId: null, pageSize: 100, "&customerId=1");

        Assert.Equal([21, 22, 23, 24, 25], page.Ids);
        Assert.Null(page.NextAfterId);
    }

    private static async Task<(List<int> Ids, int? NextAfterId)> GetCursorPageAsync(
        HttpClient client, int? afterId, int pageSize, string extra = "")
    {
        var url = $"/api/orders/cursor?pageSize={pageSize}{(afterId is null ? "" : $"&afterId={afterId}")}{extra}";

        using var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var ids = root.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("orderId").GetInt32()).ToList();
        var next = root.GetProperty("nextAfterId");

        return (ids, next.ValueKind == JsonValueKind.Null ? null : next.GetInt32());
    }

    private async Task<int> CommandsForAsync(string url)
    {
        _commands.Reset();

        using var response = await Client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        return _commands.Count;
    }
}
