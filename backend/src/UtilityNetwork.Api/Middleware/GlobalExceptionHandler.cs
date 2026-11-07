using System.Diagnostics;
using System.Security.Authentication;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace UtilityNetwork.Api.Middleware;

public sealed partial class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Unhandled exception occurred while processing request {Path} [TraceId: {TraceId}]")]
    private static partial void LogUnhandled(
        ILogger logger,
        string path,
        string traceId,
        Exception? exception);

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        var (statusCode, title, detail, errors) = exception switch
        {
            ValidationException valEx => (
                StatusCodes.Status400BadRequest,
                "Validation Error",
                "One or more validation rules were violated.",
                valEx.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            ),
            InvalidCredentialException credEx => (
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                credEx.Message,
                null
            ),
            AuthenticationException authEx => (
                StatusCodes.Status401Unauthorized,
                "Authentication Failed",
                authEx.Message,
                null
            ),
            PostgresException pgEx when pgEx.SqlState == "23505" => (
                StatusCodes.Status409Conflict,
                "Resource Conflict",
                "A resource with identical unique attributes already exists.",
                null
            ),
            PostgresException pgEx when pgEx.SqlState is "23514" or "23502" or "23503" or "22023" => (
                StatusCodes.Status400BadRequest,
                "Database Constraint Violation",
                "The requested operation violates database integrity constraints.",
                null
            ),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                _env.IsDevelopment() ? exception.Message : "An unexpected server error occurred.",
                null
            )
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            LogUnhandled(_logger, httpContext.Request.Path, traceId, exception);
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["traceId"] = traceId;
        if (errors is not null)
        {
            problemDetails.Extensions["errors"] = errors;
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken: cancellationToken);

        return true;
    }
}
