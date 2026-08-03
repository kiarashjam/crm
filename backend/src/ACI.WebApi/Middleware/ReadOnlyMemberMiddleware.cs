using ACI.Application.Interfaces;
using ACI.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Middleware;

/// <summary>
/// Enforces the read-only <see cref="OrgMemberRole.Viewer"/> role.
///
/// A Viewer may read everything in the organization but must not change anything.
/// Rather than adding a role check to every controller — which would silently miss
/// any endpoint added later — this is a single choke point: every state-changing
/// HTTP method is rejected with 403 when the caller's role in the active
/// organization is Viewer.
///
/// Must be registered AFTER authentication (so the user is resolved) and AFTER
/// <see cref="OrganizationAccessMiddleware"/> (so a forged/stale organization
/// header has already been stripped and cannot be used to dodge this check).
/// </summary>
public sealed class ReadOnlyMemberMiddleware
{
    /// <summary>
    /// Endpoints a Viewer may still write to, because they concern the viewer's own
    /// account rather than the organization's data:
    ///  - api/auth      → own password / 2FA setup
    ///  - api/settings  → own preferences (theme, notifications, defaults)
    ///  - api/joinrequests → asking to join an organization (self-service)
    ///  - api/webhook   → API-key ingestion, not a user session (never reaches here authenticated)
    /// </summary>
    private static readonly string[] AllowedPrefixes =
    {
        "/api/auth",
        "/api/settings",
        "/api/joinrequests",
        "/api/webhook",
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ReadOnlyMemberMiddleware> _logger;

    public ReadOnlyMemberMiddleware(RequestDelegate next, ILogger<ReadOnlyMemberMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentUserService currentUser,
        IOrganizationRepository organizationRepository)
    {
        if (!IsStateChanging(context.Request.Method) || IsAlwaysAllowed(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Anonymous requests are governed by [Authorize]; no org context means the
        // write can only touch the caller's own personal (non-org) records.
        if (!currentUser.IsAuthenticated
            || currentUser.UserId is not { } userId
            || currentUser.CurrentOrganizationId is not { } organizationId)
        {
            await _next(context);
            return;
        }

        var role = await organizationRepository.GetMemberRoleAsync(userId, organizationId, context.RequestAborted);
        if (role != OrgMemberRole.Viewer)
        {
            await _next(context);
            return;
        }

        // Creating a brand-new organization of their own does not modify the
        // organization they are merely a viewer of, so it stays permitted.
        if (IsCreateOwnOrganization(context.Request))
        {
            await _next(context);
            return;
        }

        _logger.LogInformation(
            "Blocked {Method} {Path} for user {UserId}: read-only (Viewer) in organization {OrganizationId}",
            context.Request.Method, context.Request.Path, userId, organizationId);

        await WriteForbiddenAsync(context);
    }

    private static bool IsStateChanging(string method) =>
        !(HttpMethods.IsGet(method)
          || HttpMethods.IsHead(method)
          || HttpMethods.IsOptions(method)
          || HttpMethods.IsTrace(method));

    private static bool IsAlwaysAllowed(PathString path) =>
        AllowedPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));

    private static bool IsCreateOwnOrganization(HttpRequest request)
    {
        if (!HttpMethods.IsPost(request.Method)) return false;
        var path = request.Path.Value?.TrimEnd('/');
        return string.Equals(path, "/api/organizations", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WriteForbiddenAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Read-only access",
            Detail = "Your role in this organization is view-only, so you cannot make changes.",
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.4",
        });
    }
}
