using Microsoft.EntityFrameworkCore;
using OrderFlow.Infrastructure.Persistence;

namespace OrderFlow.API.Extensions;

public static class DatabaseMigrationExtensions
{
    public const string SettingName = "Database:MigrateOnStartup";

    /// <summary>
    /// Applies pending EF Core migrations when <c>Database:MigrateOnStartup</c> is true. Off by default:
    /// the API should normally connect with a least-privilege login that cannot change the schema
    /// (see db/scripts/create-app-user.sql), so migrations run as a separate step. docker-compose
    /// turns it on for a one-command local setup; the connection there is the owner account.
    /// </summary>
    public static async Task MigrateDatabaseIfEnabledAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>(SettingName))
        {
            return;
        }

        using var scope = app.Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<OrderFlowDbContext>();
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigration");

        var pending = (await db.Database.GetPendingMigrationsAsync()).Count();
        logger.LogInformation("Applying {PendingCount} pending migration(s) at startup.", pending);

        await db.Database.MigrateAsync();
    }
}
