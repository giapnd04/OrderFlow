using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OrderFlow.IntegrationTests.Fixtures;

namespace OrderFlow.IntegrationTests.Products;

[Collection(SqlServerCollection.Name)]
public sealed class ProductCheckConstraintTests : IAsyncLifetime
{
    private const int CheckConstraintViolation = 547;

    private readonly SqlServerFixture _sql;

    public ProductCheckConstraintTests(SqlServerFixture sql) => _sql = sql;

    public Task InitializeAsync() => _sql.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Insert_ValidProduct_IsAccepted()
    {
        await InsertProductAsync(stockQuantity: 5, reservedQuantity: 5);

        await using var db = _sql.CreateDbContext();
        Assert.Equal(1, await db.Products.CountAsync());
    }

    [Fact]
    public async Task Insert_NegativeStockQuantity_IsRejectedByCheckConstraint()
    {
        var ex = await Assert.ThrowsAsync<SqlException>(
            () => InsertProductAsync(stockQuantity: -1, reservedQuantity: 0));

        Assert.Equal(CheckConstraintViolation, ex.Number);
        Assert.Contains("ck_products_stock_quantity", ex.Message);
    }

    [Fact]
    public async Task Insert_ReservedQuantityAboveStock_IsRejectedByCheckConstraint()
    {
        var ex = await Assert.ThrowsAsync<SqlException>(
            () => InsertProductAsync(stockQuantity: 5, reservedQuantity: 6));

        Assert.Equal(CheckConstraintViolation, ex.Number);
        Assert.Contains("ck_products_reserved_not_exceed_stock", ex.Message);
    }

    private async Task InsertProductAsync(int stockQuantity, int reservedQuantity)
    {
        await using var db = _sql.CreateDbContext();

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO products (sku, name, price, stock_quantity, reserved_quantity, status)
            VALUES ({"SKU-CHECK-1"}, {"Check constraint probe"}, {10m}, {stockQuantity}, {reservedQuantity}, {"Active"})
            """);
    }
}
