using System.Text.Json.Serialization;
using OrderFlow.API.Extensions;
using OrderFlow.API.Middleware;
using OrderFlow.Application;
using OrderFlow.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Enums travel as names ("Failed"), not numbers, in requests and responses.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddOpenApi();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services
    .AddApiAuthentication()
    .AddApiRateLimiting(builder.Configuration)
    .AddApiCors(builder.Configuration)
    .AddApiHealthChecks();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
// Covers the bodies UseStatusCodePages writes (bare 401 / 403 / 404).
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context => CorrelationIdMiddleware.AddTo(context.ProblemDetails, context.HttpContext));

var app = builder.Build();

await app.MigrateDatabaseIfEnabledAsync();
await app.SeedBootstrapAdminAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

// Order matters: the correlation id first, so every later log line (errors included) carries it;
// then errors, then the status-code pages that turn the bare 401/403/404
// from auth and routing into ProblemDetails, then CORS / rate limiting, then auth.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseHttpsRedirection();

app.UseApiCors(builder.Configuration);
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapApiHealthChecks();

app.Run();

// Lets the integration-test project host this app with WebApplicationFactory<Program>.
public partial class Program;
