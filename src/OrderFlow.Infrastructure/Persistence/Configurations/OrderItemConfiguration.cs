using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderFlow.Domain.Entities;

namespace OrderFlow.Infrastructure.Persistence.Configurations;

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items", t =>
        {
            t.HasCheckConstraint("ck_order_items_quantity", "[quantity] > 0");
            t.HasCheckConstraint("ck_order_items_unit_price", "[unit_price] >= 0");
        });

        builder.HasKey(i => i.Id);

        builder.Property(i => i.OrderId)
            .IsRequired();

        builder.Property(i => i.ProductId)
            .IsRequired();

        builder.Property(i => i.Quantity)
            .IsRequired();

        builder.Property(i => i.UnitPrice)
            .HasPrecision(18, 2);

        // Persisted computed column: the database owns this value so it can never drift
        // from quantity * unit_price (schema v1, §4). The CAST pins the store type to
        // decimal(18,2) — matching every other money column — instead of letting SQL Server
        // widen int * decimal(18,2) to decimal(29,2).
        builder.Property(i => i.Subtotal)
            .HasPrecision(18, 2)
            .HasComputedColumnSql("CAST([quantity] * [unit_price] AS decimal(18,2))", stored: true);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.OrderId, i.ProductId })
            .IsUnique();
    }
}
