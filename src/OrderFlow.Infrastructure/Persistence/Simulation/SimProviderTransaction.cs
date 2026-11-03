using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderFlow.Application.Abstractions.Payments;

namespace OrderFlow.Infrastructure.Persistence.Simulation;

/// <summary>
/// One charge in the simulated payment provider. It lives in the "sim" schema and has no
/// foreign key to orders, because it stands in for a system outside this database.
/// </summary>
internal sealed class SimProviderTransaction
{
    public int Id { get; private set; }

    public string Provider { get; private set; } = string.Empty;

    public string ProviderTransactionId { get; private set; } = string.Empty;

    public int OrderId { get; private set; }

    public decimal Amount { get; private set; }

    public DateTime CapturedAt { get; private set; }
}

internal sealed class SimProviderTransactionConfiguration : IEntityTypeConfiguration<SimProviderTransaction>
{
    public void Configure(EntityTypeBuilder<SimProviderTransaction> builder)
    {
        builder.ToTable("provider_transactions", "sim");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Provider)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(t => t.ProviderTransactionId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Amount)
            .HasPrecision(18, 2);

        builder.Property(t => t.CapturedAt)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(t => new { t.Provider, t.ProviderTransactionId })
            .IsUnique();
    }
}

internal sealed class SimPaymentProviderLedger : IPaymentProviderLedger
{
    private readonly OrderFlowDbContext _db;

    public SimPaymentProviderLedger(OrderFlowDbContext db) => _db = db;

    public async Task<IReadOnlyList<CapturedTransaction>> GetCapturedTransactionsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _db.Set<SimProviderTransaction>()
            .AsNoTracking()
            .OrderBy(t => t.CapturedAt)
            .ThenBy(t => t.Id)
            .Select(t => new CapturedTransaction(t.Provider, t.ProviderTransactionId, t.OrderId, t.Amount))
            .ToListAsync(cancellationToken);
    }
}
