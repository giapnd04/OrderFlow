using OrderFlow.IntegrationTests.Security;

namespace OrderFlow.IntegrationTests.Fixtures;

// The real API pipeline (auth, exception mapping) on top of the test SQL Server. Every HTTP
// request gets its own DI scope and therefore its own DbContext.
public sealed class SqlApiFactory(string connectionString) : ApiFactory
{
    protected override IReadOnlyDictionary<string, string> ExtraSettings { get; } = new Dictionary<string, string>
    {
        ["ConnectionStrings:OrderFlow"] = connectionString,
        // Every test request comes from the same "IP"; keep the per-IP limiter out of the result.
        ["RateLimiting:Global:PermitLimit"] = "10000",
    };
}
