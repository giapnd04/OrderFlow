using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Features.Products.GetLowStockProducts;
using OrderFlow.Domain.Enums;
using OrderFlow.IntegrationTests.Fixtures;
using OrderFlow.IntegrationTests.Security;

namespace OrderFlow.IntegrationTests.Reports;

// OF-08: a small dataset whose answers are worked out by hand below. Each report is checked
// through its endpoint against those numbers, then the script in db/scripts/reports is run on
// the same data and must agree. Dates fit the defaults declared at the top of the scripts.
[Collection(SqlServerCollection.Name)]
public sealed class ReportQueriesTests : IAsyncLifetime
{
    // Dan: order 5 (500, Dec 2025) is outside the 2026 revenue range but counts for ranking.
    // Alice: orders 1 (100, Jan) + 2 (50, Feb) = 150; the Failed attempt of 999 must not count.
    // Bob: order 3 (150, Feb) = 150, tied with Alice. Carol: order 4 (40, Mar) = 40.
    private const string SeedSql = """
        SET IDENTITY_INSERT customers ON;
        INSERT INTO customers (id, name, email, status) VALUES
            (1, N'Alice', N'alice@example.com', N'Active'),
            (2, N'Bob', N'bob@example.com', N'Active'),
            (3, N'Carol', N'carol@example.com', N'Active'),
            (4, N'Dan', N'dan@example.com', N'Active');
        SET IDENTITY_INSERT customers OFF;

        SET IDENTITY_INSERT orders ON;
        INSERT INTO orders (id, customer_id, total_amount, status, created_at, updated_at) VALUES
            (1, 1, 100, N'Paid', '2026-01-10', '2026-01-10'),
            (2, 1, 50, N'Paid', '2026-02-10', '2026-02-10'),
            (3, 2, 150, N'Paid', '2026-02-28', '2026-02-28'),
            (4, 3, 40, N'Paid', '2026-03-01', '2026-03-01'),
            (5, 4, 500, N'Paid', '2025-12-31', '2025-12-31'),
            (6, 1, 70, N'PendingPayment', '2026-03-05', '2026-03-05'),
            (7, 2, 80, N'PendingPayment', '2026-02-05', '2026-02-05'),
            (8, 3, 90, N'Cancelled', '2026-01-05', '2026-01-05'),
            (9, 3, 60, N'PendingPayment', SYSUTCDATETIME(), SYSUTCDATETIME());
        SET IDENTITY_INSERT orders OFF;

        INSERT INTO payment_attempts (order_id, amount, status, provider, provider_transaction_id, created_at, updated_at) VALUES
            (1, 100, N'Succeeded', N'P', N'T1', '2026-01-10', '2026-01-10'),
            (2, 999, N'Failed', N'P', NULL, '2026-02-10', '2026-02-10'),
            (2, 50, N'Succeeded', N'P', N'T2', '2026-02-10', '2026-02-10'),
            (3, 150, N'Succeeded', N'P', N'T3', '2026-02-28 23:59:59', '2026-02-28 23:59:59'),
            (4, 40, N'Succeeded', N'P', N'T4', '2026-03-01 00:00:00', '2026-03-01 00:00:00'),
            (5, 500, N'Succeeded', N'P', N'T5', '2025-12-31 23:59:59', '2025-12-31 23:59:59');

        SET IDENTITY_INSERT products ON;
        INSERT INTO products (id, sku, name, price, stock_quantity, reserved_quantity, status) VALUES
            (1, N'LOW-1', N'Low one', 1, 12, 7, N'Active'),
            (2, N'LOW-2', N'Low two', 1, 3, 3, N'Active'),
            (3, N'PLENTY', N'Plenty', 1, 100, 0, N'Active'),
            (4, N'GONE', N'Discontinued', 1, 0, 0, N'Discontinued');
        SET IDENTITY_INSERT products OFF;
        """;

    private readonly SqlServerFixture _sql;
    private SqlApiFactory? _factory;
    private HttpClient? _client;

    public ReportQueriesTests(SqlServerFixture sql) => _sql = sql;

    private HttpClient Client => _client ?? throw new InvalidOperationException("Not initialized.");

    public async Task InitializeAsync()
    {
        await _sql.ResetDatabaseAsync();

        await using (var db = _sql.CreateDbContext())
        {
            await db.Database.ExecuteSqlRawAsync(SeedSql);
        }

        _factory = new SqlApiFactory(_sql.ConnectionString);
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.For(UserRole.Sales));
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task MonthlyRevenue_CountsOnlySucceededWithinTheRange_AndMonthBoundariesAreExact()
    {
        var rows = await GetAsync<MonthlyRevenueRow>("/api/reports/monthly-revenue?from=2026-01-01&to=2027-01-01");

        // Jan: 100. Feb: 50 + 150 (Failed 999 ignored; 23:59:59 stays in Feb). Mar: 40 (00:00:00 is Mar).
        // December 2025 (500) is before the range.
        Assert.Equal(
            [
                new MonthlyRevenueRow(2026, 1, 1, 100m),
                new MonthlyRevenueRow(2026, 2, 2, 200m),
                new MonthlyRevenueRow(2026, 3, 1, 40m),
            ],
            rows);

        await AssertScriptAgreesAsync("monthly-revenue.sql", rows, r => [r.Year, r.Month, r.PaymentCount, r.Revenue]);
    }

    [Fact]
    public async Task TopCustomers_EqualAmountsShareARank_AndTheNextRankIsSkipped()
    {
        var rows = await GetAsync<TopCustomerRow>("/api/reports/top-customers?top=10");

        // Dan 500. Alice 100 + 50 = 150 and Bob 150 tie at rank 2. Carol 40 is rank 4, not 3.
        Assert.Equal(
            [
                new TopCustomerRow(1, 4, "Dan", 500m, 1),
                new TopCustomerRow(2, 1, "Alice", 150m, 2),
                new TopCustomerRow(2, 2, "Bob", 150m, 1),
                new TopCustomerRow(4, 3, "Carol", 40m, 1),
            ],
            rows);

        await AssertScriptAgreesAsync(
            "top-customers.sql", rows, r => [r.Rank, r.CustomerId, r.CustomerName, r.PaidAmount, r.PaymentCount]);
    }

    [Fact]
    public async Task TopCustomers_TopTwo_KeepsBothCustomersTiedAtRankTwo()
    {
        var rows = await GetAsync<TopCustomerRow>("/api/reports/top-customers?top=2");

        Assert.Equal(["Dan", "Alice", "Bob"], rows.Select(r => r.CustomerName));
    }

    [Fact]
    public async Task StalePendingOrders_OldestFirst_SkipsNewAndNonPendingOrders()
    {
        var rows = await GetAsync<StalePendingOrderRow>("/api/reports/stale-pending-orders?olderThanHours=24");

        // Order 7 (Feb) then 6 (Mar). Order 9 was created just now; 8 is Cancelled.
        Assert.Equal([7, 6], rows.Select(r => r.OrderId));
        Assert.Equal(80m, rows[0].TotalAmount);

        await AssertScriptAgreesAsync("stale-pending-orders.sql", rows, r => [r.OrderId, r.CustomerId, r.TotalAmount, r.CreatedAt]);
    }

    [Fact]
    public async Task LowStock_ScriptAgreesWithTheExistingEndpoint()
    {
        using var response = await Client.GetAsync("/api/products/low-stock?threshold=10&limit=50");
        response.EnsureSuccessStatusCode();

        var items = (await response.Content.ReadFromJsonAsync<GetLowStockProductsResult>())!.Items.ToList();

        // LOW-2: 3 - 3 = 0. LOW-1: 12 - 7 = 5. PLENTY is above the threshold, GONE is discontinued.
        Assert.Equal(["LOW-2", "LOW-1"], items.Select(i => i.Sku));

        await AssertScriptAgreesAsync("low-stock.sql", items, i => [i.ProductId, i.Sku, i.Name, i.AvailableQuantity]);
    }

    [Theory]
    [InlineData("/api/reports/monthly-revenue?from=2026-02-01&to=2026-01-01")]
    [InlineData("/api/reports/top-customers?top=0")]
    [InlineData("/api/reports/top-customers?top=101")]
    [InlineData("/api/reports/stale-pending-orders?olderThanHours=0")]
    [InlineData("/api/reports/stale-pending-orders?limit=501")]
    public async Task InvalidParameters_Return400(string url)
    {
        using var response = await Client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reports_RequireSalesOrAdministrator()
    {
        const string url = "/api/reports/top-customers";

        using var warehouse = _factory!.CreateClient();
        warehouse.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.For(UserRole.Warehouse));
        using var warehouseResponse = await warehouse.GetAsync(url);

        using var anonymous = _factory.CreateClient();
        using var anonymousResponse = await anonymous.GetAsync(url);

        Assert.Equal(HttpStatusCode.Forbidden, warehouseResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
    }

    private async Task<List<T>> GetAsync<T>(string url)
    {
        using var response = await Client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<List<T>>())!;
    }

    private async Task AssertScriptAgreesAsync<T>(string script, IReadOnlyList<T> apiRows, Func<T, object[]> columns)
    {
        await using var connection = new SqlConnection(_sql.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = await File.ReadAllTextAsync(ReportScriptPath(script));

        await using var reader = await command.ExecuteReaderAsync();

        var scriptRows = new List<object[]>();
        while (await reader.ReadAsync())
        {
            var row = new object[reader.FieldCount];
            reader.GetValues(row);
            scriptRows.Add(row);
        }

        Assert.Equal(apiRows.Select(columns).Select(Normalize), scriptRows.Select(Normalize));
    }

    // long vs int and 150.00 vs 150 are the same answer; only the printed value matters.
    private static string Normalize(object[] row) => string.Join("|", row.Select(value => value switch
    {
        decimal d => d.ToString("0.00"),
        DateTime t => t.ToString("O"),
        _ => value.ToString(),
    }));

    private static string ReportScriptPath(string script)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OrderFlow.sln")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(
            directory?.FullName ?? throw new InvalidOperationException("OrderFlow.sln not found."),
            "db", "scripts", "reports", script);
    }
}
