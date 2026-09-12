using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OrderFlow.Domain.Exceptions;
using ValidationException = OrderFlow.Application.Exceptions.ValidationException;
using NotFoundException = OrderFlow.Application.Exceptions.NotFoundException;

namespace OrderFlow.API.Middleware;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),

            InvalidOrderStateException => (StatusCodes.Status409Conflict, "Invalid order state"),

            DomainException => (StatusCodes.Status400BadRequest, "Validation failed"),

            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),

            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred"),
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {

            _logger.LogError(
                exception,
                "Unhandled exception processing {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path);
        }
        else
        {
            _logger.LogWarning(
                "{ExceptionType} while processing {Method} {Path}: {Message}",
                exception.GetType().Name,
                httpContext.Request.Method,
                httpContext.Request.Path,
                exception.Message);
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = statusCode == StatusCodes.Status500InternalServerError
                ? "An unexpected error occurred. Please try again later."
                : exception.Message,
            Instance = httpContext.Request.Path,
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
