using OrderFlow.Domain.Entities;

namespace OrderFlow.IntegrationTests.Fixtures;

public static class TestData
{
    public const int StockQuantity = 10;
    public const int OrderedQuantity = 2;
    public const decimal UnitPrice = 10m;
    public const decimal OrderTotal = OrderedQuantity * UnitPrice;

    public static async Task<(int CustomerId, int ProductId)> SeedCustomerAndProductAsync(SqlServerFixture sql)
    {
        await using var db = sql.CreateDbContext();

        var customer = Customer.Create("Test Customer", "customer@example.com");
        var product = Product.Create("SKU-TX-1", "Transaction probe", UnitPrice, StockQuantity);

        db.AddRange(customer, product);
        await db.SaveChangesAsync();

        return (customer.Id, product.Id);
    }

    // A PendingPayment order for OrderedQuantity units, with that stock already reserved.
    public static async Task<(int OrderId, int ProductId)> SeedPendingOrderAsync(SqlServerFixture sql)
    {
        var (customerId, productId) = await SeedCustomerAndProductAsync(sql);

        await using var db = sql.CreateDbContext();

        var product = await db.Products.FindAsync(productId) ?? throw new InvalidOperationException("Seed product missing.");
        product.Reserve(OrderedQuantity);

        var order = Order.Create(customerId, [OrderItem.Create(productId, OrderedQuantity, UnitPrice)]);
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return (order.Id, productId);
    }
}
