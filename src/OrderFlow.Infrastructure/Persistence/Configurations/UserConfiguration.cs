using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderFlow.Domain.Entities;

namespace OrderFlow.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", t =>
        {
            t.HasCheckConstraint(
                "ck_users_role",
                "[role] COLLATE Latin1_General_CS_AS IN (N'Customer', N'Sales', N'Warehouse', N'Administrator')");

            // Only Customer-role logins may point at a customer record (ADR-002).
            t.HasCheckConstraint(
                "ck_users_customer_link_role",
                "[customer_id] IS NULL OR [role] = N'Customer'");
        });

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(u => u.Role)
            .IsRequired()
            .HasMaxLength(32)
            .HasConversion<string>();

        builder.Property(u => u.IsEmailVerified)
            .IsRequired();

        builder.Property(u => u.CreatedAt)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(u => u.UpdatedAt)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(u => u.Email)
            .IsUnique();

        // One login per customer record; NULLs (unlinked / staff) are exempt via the filter.
        builder.HasIndex(u => u.CustomerId)
            .IsUnique()
            .HasFilter("[customer_id] IS NOT NULL");

        // Deleting a customer record (only possible when it has no orders) unlinks its login
        // instead of blocking the delete or destroying the login.
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(u => u.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
