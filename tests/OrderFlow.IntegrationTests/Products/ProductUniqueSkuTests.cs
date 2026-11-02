using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Domain.Enums;
using OrderFlow.IntegrationTests.Fixtures;
using OrderFlow.IntegrationTests.Security;

namespace OrderFlow.IntegrationTests.Products;

// OF-02: the handler's ExistsBySku check covers the normal case; when two requests pass that
// check at once, only the unique index on sku can stop the second one.
[Collection(SqlServerCollection.Name)]
public sealed class ProductUniqueSkuTests : IAsyncLifetime
{
    private readonly SqlServerFixture _sql;
    private SqlApiFactory? _factory;
    private HttpClient? _client;

    public ProductUniqueSkuTests(SqlServerFixture sql) => _sql = sql;

    private HttpClient Client => _client ?? throw new InvalidOperationException("Not initialized.");

    public async Task InitializeAsync()
    {
        await _sql.ResetDatabaseAsync();

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
    public async Task CreateProduct_SameSkuTwice_SecondGets409()
    {
        Assert.Equal(HttpStatusCode.Created, await CreateProductAsync());
        Assert.Equal(HttpStatusCode.Conflict, await CreateProductAsync());

        await using var db = _sql.CreateDbContext();
        Assert.Equal(1, await db.Products.CountAsync());
    }

    [Fact]
    public async Task CreateProduct_SameSkuConcurrently_ExactlyOneWins()
    {
        var results = await Concurrently.RunAsync(2, CreateProductAsync);

        Assert.Equal(1, results.Count(status => status == HttpStatusCode.Created));
        Assert.Equal(1, results.Count(status => status == HttpStatusCode.Conflict));

        await using var db = _sql.CreateDbContext();
        Assert.Equal(1, await db.Products.CountAsync());
    }

    private async Task<HttpStatusCode> CreateProductAsync()
    {
        using var response = await Client.PostAsJsonAsync("/api/products", new
        {
            sku = "SKU-UNIQUE-1",
            name = "Unique SKU probe",
            price = 10m,
            stockQuantity = 1,
        });

        return response.StatusCode;
    }
}
