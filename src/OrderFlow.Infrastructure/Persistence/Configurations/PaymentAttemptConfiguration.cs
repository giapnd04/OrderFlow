using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderFlow.Domain.Entities;

namespace OrderFlow.Infrastructure.Persistence.Configurations;

internal sealed class PaymentAttemptConfiguration : IEntityTypeConfiguration<PaymentAttempt>
{
    public void Configure(EntityTypeBuilder<PaymentAttempt> builder)
    {
        builder.ToTable("payment_attempts", t =>
        {
            t.HasCheckConstraint("ck_payment_attempts_amount", "[amount] > 0");
            t.HasCheckConstraint(
                "ck_payment_attempts_status",
                "[status] COLLATE Latin1_General_CS_AS IN (N'Pending', N'Succeeded', N'Failed', N'Cancelled', N'Expired')");
        });

        builder.HasKey(p => p.Id);

        builder.Property(p => p.OrderId)
            .IsRequired();

        builder.Property(p => p.Amount)
            .HasPrecision(18, 2);

        builder.Property(p => p.Status)
            .IsRequired()
            .HasMaxLength(32)
            .HasConversion<string>();

        builder.Property(p => p.Provider)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(p => p.ProviderTransactionId)
            .HasMaxLength(200);

        builder.Property(p => p.CreatedAt)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(p => p.UpdatedAt)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(p => p.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        // DB-level idempotency guard for provider webhooks (schema v1, §5).
        builder.HasIndex(p => new { p.Provider, p.ProviderTransactionId })
            .IsUnique()
            .HasFilter("[provider_transaction_id] IS NOT NULL");
    }
}
