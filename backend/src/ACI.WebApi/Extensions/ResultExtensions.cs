using ACI.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Extensions;

/// <summary>
/// Extension methods to convert Result types to ActionResult responses.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Converts a Result to an appropriate ActionResult.
    /// </summary>
    public static ActionResult ToActionResult(this Result result)
    {
        if (result.IsSuccess)
            return new OkResult();

        return ToProblemDetails(result.Error);
    }

    /// <summary>
    /// Converts a Result&lt;T&gt; to an appropriate ActionResult.
    /// </summary>
    public static ActionResult<T> ToActionResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
            return new OkObjectResult(result.Value);

        return ToProblemDetails(result.Error);
    }

    /// <summary>
    /// Converts a Result&lt;T&gt; to an appropriate ActionResult, returning 201 Created on success.
    /// </summary>
    public static ActionResult<T> ToCreatedResult<T>(this Result<T> result, string actionName, object? routeValues = null)
    {
        if (result.IsSuccess)
            return new CreatedAtActionResult(actionName, null, routeValues, result.Value);

        return ToProblemDetails(result.Error);
    }

    /// <summary>
    /// Converts a Result to NoContent (204) on success.
    /// </summary>
    public static ActionResult ToNoContentResult(this Result result)
    {
        if (result.IsSuccess)
            return new NoContentResult();

        return ToProblemDetails(result.Error);
    }

    /// <summary>
    /// Converts an Error to a ProblemDetails ObjectResult.
    /// </summary>
    private static ObjectResult ToProblemDetails(Error error)
    {
        var statusCode = GetStatusCode(error.Code);
        
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = GetTitle(statusCode),
            Detail = error.Description,
            Type = GetProblemType(statusCode)
        };

        problemDetails.Extensions["errorCode"] = error.Code;

        return new ObjectResult(problemDetails)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" }
        };
    }

    /// <summary>
    /// Maps error codes to HTTP status codes.
    /// </summary>
    private static int GetStatusCode(string errorCode) => errorCode switch
    {
        // Not Found errors
        var code when code.EndsWith(".NotFound") => StatusCodes.Status404NotFound,
        
        // Unauthorized/Authentication errors
        "Auth.InvalidCredentials" => StatusCodes.Status401Unauthorized,
        "Auth.EmailNotFound" => StatusCodes.Status401Unauthorized,
        "Auth.InvalidTwoFactorCode" => StatusCodes.Status401Unauthorized,
        "Auth.AccountLocked" => StatusCodes.Status401Unauthorized,
        "General.Unauthorized" => StatusCodes.Status401Unauthorized,
        
        // Forbidden errors
        var code when code.Contains("NotOwner") => StatusCodes.Status403Forbidden,
        var code when code.Contains("NotMember") => StatusCodes.Status403Forbidden,
        var code when code.Contains("CannotRemove") => StatusCodes.Status403Forbidden,
        var code when code.Contains("CannotChange") => StatusCodes.Status403Forbidden,
        var code when code.Contains("CannotAssign") => StatusCodes.Status403Forbidden,
        "General.Forbidden" => StatusCodes.Status403Forbidden,
        "Template.NotOwned" => StatusCodes.Status403Forbidden,
        "Template.CannotModifySystem" => StatusCodes.Status403Forbidden,
        
        // Conflict errors
        var code when code.Contains("Duplicate") => StatusCodes.Status409Conflict,
        var code when code.Contains("AlreadyExists") => StatusCodes.Status409Conflict,
        var code when code.Contains("AlreadyMember") => StatusCodes.Status409Conflict,
        var code when code.Contains("AlreadyConverted") => StatusCodes.Status409Conflict,
        var code when code.Contains("AlreadyAccepted") => StatusCodes.Status409Conflict,
        var code when code.Contains("AlreadyArchived") => StatusCodes.Status409Conflict,
        var code when code.Contains("AlreadyPending") => StatusCodes.Status409Conflict,
        var code when code.Contains("AlreadyProcessed") => StatusCodes.Status409Conflict,
        "Error.Conflict" => StatusCodes.Status409Conflict,
        
        // Bad Request errors (default for validation/business rule violations)
        _ => StatusCodes.Status400BadRequest
    };

    private static string GetTitle(int statusCode) => statusCode switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        500 => "Server Error",
        _ => "Error"
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
