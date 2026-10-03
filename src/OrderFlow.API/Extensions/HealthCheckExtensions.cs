using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrderFlow.Infrastructure.Persistence;

namespace OrderFlow.API.Extensions;

public static class HealthCheckExtensions
{
    private const string ReadyTag = "ready";

    public static IServiceCollection AddApiHealthChecks(this IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .AddDbContextCheck<OrderFlowDbContext>("database", tags: new[] { ReadyTag });

        return services;
    }

    /// <summary>
    /// <c>/health/live</c>: the process is up (no dependencies checked - restart if this fails).
    /// <c>/health/ready</c>: dependencies are reachable (database) - stop routing traffic if this fails.
    /// Both are anonymous so orchestrators can probe them without credentials.
    /// </summary>
    public static IEndpointRouteBuilder MapApiHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
            .AllowAnonymous();

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ReadyTag),
        }).AllowAnonymous();

        return endpoints;
    }
}
