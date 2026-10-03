using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Domain.Exceptions;
using AuthenticationFailedException = OrderFlow.Application.Exceptions.AuthenticationFailedException;
using ConflictException = OrderFlow.Application.Exceptions.ConflictException;
using ForbiddenException = OrderFlow.Application.Exceptions.ForbiddenException;
using NotFoundException = OrderFlow.Application.Exceptions.NotFoundException;
using ValidationException = OrderFlow.Application.Exceptions.ValidationException;

namespace OrderFlow.API.Middleware;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    // SQL Server error numbers: duplicate key / unique violation, and foreign-key violation.
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;
    private const int ForeignKeyViolation = 547;

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
        var (statusCode, title, detail) = Map(exception);

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
            Detail = detail,
            Instance = httpContext.Request.Path,
        };

        if (exception is ValidationException { Errors.Count: > 0 } validation)
        {
            problemDetails.Extensions["errors"] = validation.Errors;
        }

        if (exception is AuthenticationFailedException)
        {
            httpContext.Response.Headers.WWWAuthenticate = "Bearer";
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            options: null,
            contentType: "application/problem+json",
            cancellationToken);

        return true;
    }

    private static (int StatusCode, string Title, string Detail) Map(Exception exception) => exception switch
    {
        ValidationException => (StatusCodes.Status400BadRequest, "Validation failed", exception.Message),

        AuthenticationFailedException => (StatusCodes.Status401Unauthorized, "Authentication failed", exception.Message),

        ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden", exception.Message),

        InvalidOrderStateException => (StatusCodes.Status409Conflict, "Invalid order state", exception.Message),

        InsufficientStockException => (StatusCodes.Status409Conflict, "Insufficient stock", exception.Message),

        ConflictException => (StatusCodes.Status409Conflict, "Conflict", exception.Message),

        DomainException => (StatusCodes.Status400BadRequest, "Validation failed", exception.Message),

        NotFoundException => (StatusCodes.Status404NotFound, "Resource not found", exception.Message),

        // Optimistic concurrency (rowversion on Order / Product): someone else changed the row first.
        DbUpdateConcurrencyException => (
            StatusCodes.Status409Conflict,
            "Concurrent modification",
            "The resource was modified by another request. Reload it and try again."),

        // Last line of defence behind the handlers' check-then-act guards (two requests racing
        // on the same unique value, or touching rows other rows still reference). The SQL
        // message is never echoed back - it names tables and constraints.
        DbUpdateException { InnerException: SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation } } => (
            StatusCodes.Status409Conflict,
            "Duplicate value",
            "A record with the same unique value already exists."),

        DbUpdateException { InnerException: SqlException { Number: ForeignKeyViolation } } => (
            StatusCodes.Status409Conflict,
            "Conflict with related data",
            "The operation conflicts with related data."),

        _ => (
            StatusCodes.Status500InternalServerError,
            "An unexpected error occurred",
            "An unexpected error occurred. Please try again later."),
    };
}
