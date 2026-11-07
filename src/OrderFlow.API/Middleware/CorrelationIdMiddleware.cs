using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;

namespace OrderFlow.API.Middleware;

/// <summary>
/// Gives every request one id that ties its log lines together (OF-10). A caller's
/// X-Correlation-Id is reused so a request can be followed across services; anything missing or
/// malformed is replaced, because the value is written into logs and must not carry injected text.
/// The id is echoed in the response header and in every ProblemDetails body.
/// </summary>
public sealed partial class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].ToString() is { } incoming && IsValid(incoming)
            ? incoming
            : Guid.NewGuid().ToString("N");

        // Everything that already reports TraceIdentifier (the framework's own logs, ProblemDetails
        // "traceId") now reports the same id.
        context.TraceIdentifier = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        // A message-template scope gives log sinks both a readable "CorrelationId:..." text and a
        // separate CorrelationId field to filter on.
        using (_logger.BeginScope("CorrelationId:{CorrelationId}", correlationId))
        {
            await _next(context);
        }
    }

    public static void AddTo(ProblemDetails problem, HttpContext context)
        => problem.Extensions["correlationId"] = context.TraceIdentifier;

    private static bool IsValid(string value) => value.Length is > 0 and <= 64 && SafeId().IsMatch(value);

    [GeneratedRegex("^[A-Za-z0-9._:-]+$")]
    private static partial Regex SafeId();
}
