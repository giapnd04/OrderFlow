using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Domain.Enums;
using OrderFlow.IntegrationTests.Fixtures;

namespace OrderFlow.IntegrationTests.Security;

// OF-07: what the API and the database refuse once a request is past authentication.
[Collection(SqlServerCollection.Name)]
public sealed class DataAccessSecurityTests : IAsyncLifetime
{
    private readonly SqlServerFixture _sql;
    private SqlApiFactory? _factory;

    public DataAccessSecurityTests(SqlServerFixture sql) => _sql = sql;

    private SqlApiFactory Factory => _factory ?? throw new InvalidOperationException("Not initialized.");

    public async Task InitializeAsync()
    {
        await _sql.ResetDatabaseAsync();
        _factory = new SqlApiFactory(_sql.ConnectionString);
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Theory]
    [InlineData("' OR 1=1 --")]
    [InlineData("%' OR '1'='1")]
    [InlineData("'; DROP TABLE products; --")]
    public async Task SearchWithSqlInjectionPayload_IsTreatedAsPlainText(string payload)
    {
        await TestData.SeedCustomerAndProductAsync(_sql);
        using var client = ClientFor(UserRole.Sales);

        using var response = await client.GetAsync($"/api/products?search={Uri.EscapeDataString(payload)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("totalCount").GetInt32());

        await using var db = _sql.CreateDbContext();
        Assert.Equal(1, await db.Products.CountAsync());
    }

    [Fact]
    public async Task Customer_GetsOwnOrder_But404ForSomeoneElses()
    {
        var (customerId, productId) = await TestData.SeedCustomerAndProductAsync(_sql);
        var otherCustomerId = await SeedOtherCustomerAsync();
        var orderId = await CreateOrderAsync(customerId, productId);

        using var owner = ClientFor(UserRole.Customer, customerId);
        using var stranger = ClientFor(UserRole.Customer, otherCustomerId);

        using var own = await owner.GetAsync($"/api/orders/{orderId}");
        using var foreign = await stranger.GetAsync($"/api/orders/{orderId}");
        using var missing = await stranger.GetAsync("/api/orders/999999");

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);

        // Identical to an order that does not exist, so ids cannot be probed.
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task AppRoles_CanReadAndWriteData_ButCannotChangeTheSchema()
    {
        await TestData.SeedCustomerAndProductAsync(_sql);

        // No pooling: a batch that fails inside EXECUTE AS must not hand its identity to anyone.
        var connectionString = new SqlConnectionStringBuilder(_sql.ConnectionString) { Pooling = false }.ConnectionString;

        await ExecuteAsync(connectionString, """
            IF USER_ID('least_privilege_probe') IS NOT NULL DROP USER least_privilege_probe;
            CREATE USER least_privilege_probe WITHOUT LOGIN;
            ALTER ROLE db_datareader ADD MEMBER least_privilege_probe;
            ALTER ROLE db_datawriter ADD MEMBER least_privilege_probe;
            """);

        try
        {
            await ExecuteAsProbeAsync(connectionString, "SELECT COUNT(*) FROM products;");
            await ExecuteAsProbeAsync(connectionString, "UPDATE products SET updated_at = SYSUTCDATETIME();");
            await ExecuteAsProbeAsync(connectionString, "SELECT COUNT(*) FROM sim.provider_transactions;");

            await Assert.ThrowsAsync<SqlException>(() => ExecuteAsProbeAsync(connectionString, "DROP TABLE products;"));
            await Assert.ThrowsAsync<SqlException>(() => ExecuteAsProbeAsync(connectionString, "ALTER TABLE products ADD probe int NULL;"));
            await Assert.ThrowsAsync<SqlException>(() => ExecuteAsProbeAsync(connectionString, "CREATE TABLE probe_table (id int);"));

            await using var db = _sql.CreateDbContext();
            Assert.Equal(1, await db.Products.CountAsync());
        }
        finally
        {
            await ExecuteAsync(connectionString, "DROP USER least_privilege_probe;");
        }
    }

    private HttpClient ClientFor(UserRole role, int? customerId = null)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.For(role, customerId));
        return client;
    }

    private async Task<int> CreateOrderAsync(int customerId, int productId)
    {
        using var client = ClientFor(UserRole.Sales);
        using var response = await client.PostAsJsonAsync("/api/orders", new
        {
            customerId,
            items = new[] { new { productId, quantity = 1 } },
        });

        response.EnsureSuccessStatusCode();

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("orderId").GetInt32();
    }

    private async Task<int> SeedOtherCustomerAsync()
    {
        await using var db = _sql.CreateDbContext();
        var customer = Domain.Entities.Customer.Create("Other Customer", "other@example.com");
        db.Add(customer);
        await db.SaveChangesAsync();
        return customer.Id;
    }

    private static Task ExecuteAsProbeAsync(string connectionString, string sql)
        => ExecuteAsync(connectionString, $"EXECUTE AS USER = 'least_privilege_probe'; {sql} REVERT;");

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
