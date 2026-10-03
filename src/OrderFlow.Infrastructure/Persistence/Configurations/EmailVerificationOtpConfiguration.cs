using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderFlow.Domain.Entities;

namespace OrderFlow.Infrastructure.Persistence.Configurations;

internal sealed class EmailVerificationOtpConfiguration : IEntityTypeConfiguration<EmailVerificationOtp>
{
    public void Configure(EntityTypeBuilder<EmailVerificationOtp> builder)
    {
        builder.ToTable("email_verification_otps", t =>
        {
            t.HasCheckConstraint(
                "ck_email_verification_otps_code",
                "LEN([code]) = 6 AND [code] NOT LIKE '%[^0-9]%'");
        });

        builder.HasKey(o => o.Id);

        builder.Property(o => o.UserId)
            .IsRequired();

        builder.Property(o => o.Code)
            .IsRequired()
            .HasMaxLength(EmailVerificationOtp.CodeLength);

        builder.Property(o => o.ExpiresAt)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.Property(o => o.ConsumedAt)
            .HasColumnType("datetime2");

        builder.Property(o => o.CreatedAt)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(o => o.UpdatedAt)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(o => o.UserId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
