using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Infrastructure.Persistence;
using OrderFlow.IntegrationTests.Security;

namespace OrderFlow.IntegrationTests.Fixtures;

// The real API pipeline (auth, exception mapping) on top of the test SQL Server. Every HTTP
// request gets its own DI scope and therefore its own DbContext. Pass a counter to count the
// SQL commands each request sends.
public sealed class SqlApiFactory(string connectionString, SqlCommandCounter? commandCounter = null) : ApiFactory
{
    protected override IReadOnlyDictionary<string, string> ExtraSettings { get; } = new Dictionary<string, string>
    {
        ["ConnectionStrings:OrderFlow"] = connectionString,
        // Every test request comes from the same "IP"; keep the per-IP limiter out of the result.
        ["RateLimiting:Global:PermitLimit"] = "10000",
        ["RateLimiting:Auth:PermitLimit"] = "10000",
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        if (commandCounter is not null)
        {
            builder.ConfigureTestServices(services =>
                services.ConfigureDbContext<OrderFlowDbContext>(options => options.AddInterceptors(commandCounter)));
        }
    }
}
