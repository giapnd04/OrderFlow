using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderFlow.Application.Abstractions.Security;
using OrderFlow.Infrastructure.Persistence;

namespace OrderFlow.Infrastructure.Authentication;

internal sealed class LoginThrottle
{
    public string Email { get; private set; } = string.Empty;

    public int FailedCount { get; private set; }

    public DateTime WindowStartedAt { get; private set; }

    public DateTime? LockedUntil { get; private set; }
}

internal sealed class LoginThrottleConfiguration : IEntityTypeConfiguration<LoginThrottle>
{
    public void Configure(EntityTypeBuilder<LoginThrottle> builder)
    {
        builder.ToTable("login_throttles");

        builder.HasKey(t => t.Email);

        // Same length as users.email: this is the normalized login address.
        builder.Property(t => t.Email)
            .HasMaxLength(256);

        builder.Property(t => t.WindowStartedAt)
            .HasColumnType("datetime2");

        builder.Property(t => t.LockedUntil)
            .HasColumnType("datetime2");
    }
}

internal sealed class SqlLoginThrottleStore : ILoginThrottleStore
{
    private readonly OrderFlowDbContext _db;

    public SqlLoginThrottleStore(OrderFlowDbContext db) => _db = db;

    public async Task<DateTime?> GetLockedUntilAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _db.Set<LoginThrottle>()
            .AsNoTracking()
            .Where(t => t.Email == email)
            .Select(t => t.LockedUntil)
            .FirstOrDefaultAsync(cancellationToken);
    }

    // One atomic statement: parallel failures from many IPs must each count, which a
    // read-then-write would lose. HOLDLOCK closes MERGE's gap when the row does not exist yet.
    public async Task RecordFailureAsync(string email, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var windowStart = nowUtc - LoginThrottlePolicy.FailureWindow;
        var lockedUntil = nowUtc + LoginThrottlePolicy.LockDuration;

        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            MERGE login_throttles WITH (HOLDLOCK) AS t
            USING (SELECT {email} AS email) AS s ON t.email = s.email
            WHEN MATCHED THEN UPDATE SET
                failed_count = CASE WHEN t.window_started_at < {windowStart} THEN 1 ELSE t.failed_count + 1 END,
                window_started_at = CASE WHEN t.window_started_at < {windowStart} THEN {nowUtc} ELSE t.window_started_at END,
                locked_until = CASE
                    WHEN t.window_started_at < {windowStart} THEN NULL
                    WHEN t.failed_count + 1 >= {LoginThrottlePolicy.MaxFailures} THEN {lockedUntil}
                    ELSE t.locked_until END
            WHEN NOT MATCHED THEN
                INSERT (email, failed_count, window_started_at) VALUES (s.email, 1, {nowUtc});
            """, cancellationToken);
    }

    public Task ResetAsync(string email, CancellationToken cancellationToken = default)
        => _db.Set<LoginThrottle>().Where(t => t.Email == email).ExecuteDeleteAsync(cancellationToken);
}
