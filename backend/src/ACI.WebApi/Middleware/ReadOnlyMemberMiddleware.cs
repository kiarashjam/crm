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
    private const StringComparison Ignore = StringComparison.OrdinalIgnoreCase;

    /// <summary>
    /// Own-account areas a Viewer may always write to: credentials / 2FA, and their
    /// personal preferences (theme, notifications, defaults). Neither touches
    /// organization data.
    /// </summary>
    private static readonly string[] OwnAccountPrefixes =
    {
        "/api/auth",
        "/api/settings",
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
        if (!IsStateChanging(context.Request.Method) || IsSelfServiceWrite(context.Request))
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

    /// <summary>
    /// Writes that only ever affect the caller's own account or their own membership,
    /// so they stay open to a Viewer. Everything else — including administering the
    /// organization they view — is refused.
    ///
    /// Deliberately matched precisely rather than by controller prefix: several
    /// administrative routes live under otherwise self-service controllers
    /// (creating an invite, accepting/rejecting someone else's join request,
    /// rotating the organization's webhook key), and a blanket prefix would exempt
    /// exactly the actions a read-only member must not perform.
    /// </summary>
    private static bool IsSelfServiceWrite(HttpRequest request)
    {
        if (OwnAccountPrefixes.Any(prefix => request.Path.StartsWithSegments(prefix, Ignore)))
        {
            return true;
        }

        // Everything below is a POST; nothing else is self-service.
        if (!HttpMethods.IsPost(request.Method)) return false;

        var path = request.Path.Value?.TrimEnd('/') ?? string.Empty;

        // Creating a brand-new organization of their own does not change the
        // organization they are merely a viewer of.
        if (string.Equals(path, "/api/organizations", Ignore)) return true;

        // Accepting an invitation — /api/invites/accept or /api/invites/{id}/accept —
        // concerns only their own membership. Creating an invite
        // (/api/invites/{organizationId}) is administrative and is NOT included.
        if (path.StartsWith("/api/invites/", Ignore) && path.EndsWith("/accept", Ignore)) return true;

        // Asking to join an organization: /api/joinrequests/{organizationId}.
        // Accepting or rejecting somebody else's request is administrative.
        if (path.StartsWith("/api/joinrequests/", Ignore)
            && !path.EndsWith("/accept", Ignore)
            && !path.EndsWith("/reject", Ignore))
        {
            return true;
        }

        return false;
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
