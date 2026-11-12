using ACI.Application.Common;
using ACI.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Middleware;

/// <summary>
/// A workspace that requires single sign-on refuses, with 403 (errorCode Sso.Required), every
/// request into it from a session that did not begin with single sign-on, for the members the
/// requirement holds (see IOrganizationSso).
/// </summary>
/// <remarks>
/// <para>
/// One choke point for everything that names the workspace in X-Organization-Id, so an endpoint
/// added later cannot forget the check. Endpoints that name a workspace in their route
/// (/api/organizations/{id}/…, its keys and invitations) are checked by
/// ActsForRouteOrganizationAttribute.
/// </para>
/// <para>
/// Must follow <see cref="OrganizationAccessMiddleware"/>, so a forged header has been dropped
/// already. The person's own account (sign-in, 2FA, passkeys, preferences), the list of their
/// workspaces and their own invitations stay open: from there they sign in again with single
/// sign-on, or pick another workspace.
/// </para>
/// </remarks>
public sealed class SsoRequirementMiddleware
{
    private const StringComparison Ignore = StringComparison.OrdinalIgnoreCase;

    private static readonly string[] OwnAccountPrefixes = ["/api/auth", "/api/settings"];

    private static readonly string[] OwnAccountPaths = ["/api/organizations", "/api/invites/pending", "/api/invites/accept"];

    private readonly RequestDelegate _next;
    private readonly ILogger<SsoRequirementMiddleware> _logger;

    public SsoRequirementMiddleware(RequestDelegate next, ILogger<SsoRequirementMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentUserService currentUser, IOrganizationSso sso)
    {
        if (currentUser.IsAuthenticated
            && currentUser.UserId is { } userId
            && currentUser.CurrentOrganizationId is { } organizationId
            && !IsOwnAccount(context.Request))
        {
            var method = SignInMethods.Of(context.User);
            if (await sso.RefusalAsync(organizationId, userId, method, context.RequestAborted) is { } workspace)
            {
                _logger.LogInformation(
                    "Refused {Method} {Path} for user {UserId}: organization {OrganizationId} requires single sign-on and the session began with {SignIn}",
                    context.Request.Method, context.Request.Path, userId, organizationId, method);
                await WriteRefusalAsync(context, workspace, organizationId);
                return;
            }
        }

        await _next(context);
    }

    private static bool IsOwnAccount(HttpRequest request)
    {
        if (OwnAccountPrefixes.Any(prefix => request.Path.StartsWithSegments(prefix, Ignore))) return true;
        var path = request.Path.Value?.TrimEnd('/') ?? "";
        return OwnAccountPaths.Any(p => string.Equals(path, p, Ignore));
    }

    /// <summary>The same problem shape as every other refusal, naming the workspace so the app can offer the way in.</summary>
    public static async Task WriteRefusalAsync(HttpContext context, string workspace, Guid organizationId)
    {
        var error = DomainErrors.Sso.Required(workspace);
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Single sign-on required",
            Detail = error.Description,
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.4",
        };
        problem.Extensions["errorCode"] = error.Code;
        problem.Extensions["organizationId"] = organizationId;
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
    }
}
