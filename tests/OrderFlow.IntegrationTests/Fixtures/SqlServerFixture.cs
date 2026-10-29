using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Application;
using OrderFlow.Infrastructure;
using OrderFlow.Infrastructure.Persistence;
using OrderFlow.IntegrationTests.Security;
using Respawn;
using Testcontainers.MsSql;
using Xunit.Abstractions;

namespace OrderFlow.IntegrationTests.Fixtures;

public sealed class SqlServerFixture : IAsyncLifetime
{
    public const string ConnectionVariable = "ORDERFLOW_TEST_CONNECTION";

    private const string SqlServerImage = "mcr.microsoft.com/mssql/server:2022-latest";

    private MsSqlContainer? _container;
    private Respawner? _respawner;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        ConnectionString = Environment.GetEnvironmentVariable(ConnectionVariable) is { Length: > 0 } configured
            ? EnsureTestDatabase(configured)
            : await StartContainerAsync();

        await using (var db = CreateDbContext())
        {
            await db.Database.MigrateAsync();
        }

        await using var connection = await OpenConnectionAsync();

        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            TablesToIgnore = ["__EFMigrationsHistory"],
        });
    }

    public OrderFlowDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OrderFlowDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new OrderFlowDbContext(options);
    }

    // The production composition root (AddApplication + AddInfrastructure) pointed at the test
    // database, so handlers resolve with their real repositories and ITransactionRunner.
    public ServiceProvider CreateServiceProvider(ITestOutputHelper? sqlLog = null, params IInterceptor[] interceptors)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:OrderFlow"] = ConnectionString,
                ["Jwt:SigningKey"] = ApiFactory.SigningKey,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);

        services.ConfigureDbContext<OrderFlowDbContext>(options =>
        {
            options.AddInterceptors(interceptors);

            if (sqlLog is not null)
            {
                options.EnableSensitiveDataLogging()
                    .LogTo(sqlLog.WriteLine, [RelationalEventId.CommandExecuted, RelationalEventId.TransactionStarted,
                        RelationalEventId.TransactionCommitted, RelationalEventId.TransactionRolledBack,
                        RelationalEventId.TransactionDisposed]);
            }
        });

        return services.BuildServiceProvider(validateScopes: true);
    }

    /// <summary>Deletes all rows (FK-ordered) but keeps the schema and migration history.</summary>
    public async Task ResetDatabaseAsync()
    {
        if (_respawner is null)
        {
            throw new InvalidOperationException("SqlServerFixture has not been initialized.");
        }

        await using var connection = await OpenConnectionAsync();
        await _respawner.ResetAsync(connection);
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    private async Task<string> StartContainerAsync()
    {
        try
        {
            // Build() already probes Docker, so it belongs inside the try as well.
            _container = new MsSqlBuilder(SqlServerImage).Build();
            await _container.StartAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not start a SQL Server container ({ex.GetType().Name}: {ex.Message}). " +
                $"Start Docker, or set {ConnectionVariable} to a test database, e.g. " +
                @"Server=(localdb)\MSSQLLocalDB;Database=OrderFlow_IntegrationTests;Trusted_Connection=True;TrustServerCertificate=True",
                ex);
        }

        return _container.GetConnectionString();
    }

    private async Task<SqlConnection> OpenConnectionAsync()
    {
        var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    // Respawn deletes every row, so refuse anything that could be the dev or a shared database.
    private static string EnsureTestDatabase(string connectionString)
    {
        var database = new SqlConnectionStringBuilder(connectionString).InitialCatalog;

        if (!database.Contains("Test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{ConnectionVariable} points at database '{database}'. Integration tests wipe all data, " +
                "so the database name must contain 'Test'.");
        }

        return connectionString;
    }
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}
