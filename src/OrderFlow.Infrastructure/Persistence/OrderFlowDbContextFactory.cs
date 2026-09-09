using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OrderFlow.Infrastructure.Persistence;

/// <summary>
/// Used by the EF Core CLI (<c>dotnet ef migrations</c> / <c>database update</c>) to build the
/// context at design time, since Infrastructure is a class library with no host.
/// The connection string defaults to the local development LocalDB instance and can be
/// overridden with the <c>ORDERFLOW_CONNECTION</c> environment variable. It carries no secret,
/// so it is safe to keep in source control (schema v1, §12).
/// </summary>
public sealed class OrderFlowDbContextFactory : IDesignTimeDbContextFactory<OrderFlowDbContext>
{
    private const string DefaultConnectionString =
        @"Server=(localdb)\MSSQLLocalDB;Database=OrderFlow;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    public OrderFlowDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ORDERFLOW_CONNECTION") ?? DefaultConnectionString;

        var options = new DbContextOptionsBuilder<OrderFlowDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(OrderFlowDbContext).Assembly.FullName))
            .Options;

        return new OrderFlowDbContext(options);
    }
}
