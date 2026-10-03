namespace OrderFlow.API.Extensions;

public static class CorsExtensions
{
    public const string PolicyName = "OrderFlowCors";

    /// <summary>
    /// CORS is opt-in: browsers are only allowed from origins listed in <c>Cors:AllowedOrigins</c>.
    /// With none configured no policy is added, so cross-origin browser calls are rejected
    /// (same-origin and non-browser clients are unaffected). There is no wildcard fallback.
    /// </summary>
    public static IServiceCollection AddApiCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

        if (origins.Length == 0)
        {
            return services;
        }

        services.AddCors(options => options.AddPolicy(PolicyName, policy => policy
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()));

        return services;
    }

    public static IApplicationBuilder UseApiCors(this IApplicationBuilder app, IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

        return origins.Length == 0 ? app : app.UseCors(PolicyName);
    }
}
