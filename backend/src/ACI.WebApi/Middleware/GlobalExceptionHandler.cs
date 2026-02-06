using System.Text.Json;
using ACI.Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Middleware;

/// <summary>
/// Global exception handler that catches unhandled exceptions and returns standardized ProblemDetails responses.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, detail) = MapException(exception);

        _logger.LogError(
            exception,
            "Unhandled exception occurred. TraceId: {TraceId}, Path: {Path}, Method: {Method}, StatusCode: {StatusCode}",
            httpContext.TraceIdentifier,
            httpContext.Request.Path,
            httpContext.Request.Method,
            statusCode);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = _environment.IsDevelopment() ? exception.Message : detail,
            Instance = httpContext.Request.Path,
            Type = GetProblemType(statusCode)
        };

        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;
        
        if (_environment.IsDevelopment())
        {
            problemDetails.Extensions["exception"] = new
            {
                type = exception.GetType().Name,
                message = exception.Message,
                stackTrace = exception.StackTrace?.Split(Environment.NewLine)
            };
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static (int StatusCode, string Title, string Detail) MapException(Exception exception) => exception switch
    {
        UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Forbidden", "You are not authorized to perform this action"),
        InvalidOperationException => (StatusCodes.Status400BadRequest, "Bad Request", "The request was invalid"),
        ArgumentException => (StatusCodes.Status400BadRequest, "Bad Request", "Invalid argument provided"),
        KeyNotFoundException => (StatusCodes.Status404NotFound, "Not Found", "The requested resource was not found"),
        OperationCanceledException => (StatusCodes.Status499ClientClosedRequest, "Request Cancelled", "The request was cancelled"),
        _ => (StatusCodes.Status500InternalServerError, "Server Error", "An unexpected error occurred")
    };

    private static string GetProblemType(int statusCode) => statusCode switch
    {
        400 => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
        401 => "https://tools.ietf.org/html/rfc7235#section-3.1",
        403 => "https://tools.ietf.org/html/rfc7231#section-6.5.3",
        404 => "https://tools.ietf.org/html/rfc7231#section-6.5.4",
        409 => "https://tools.ietf.org/html/rfc7231#section-6.5.8",
        500 => "https://tools.ietf.org/html/rfc7231#section-6.6.1",
        _ => "https://tools.ietf.org/html/rfc7231#section-6.6.1"
    };
}
