using ACI.Application.Interfaces;

namespace ACI.WebApi.Middleware;

/// <summary>
/// Verifies that the authenticated user is a member of the organization indicated
/// by the X-Organization-Id header. Without this check, any authenticated user
/// could read or modify data in any organization by setting the header.
///
/// When the header is set to an org the user is not a member of, we silently
/// remove the header rather than 403. This handles the case where a user has a
/// stale org id in their client-side state (e.g. they were removed from an org
/// since their last login). Without this, every request — including the org-list
/// endpoint the client uses to recover — would 403, locking the user out.
///
/// Removing the header is also the right answer for a forged header: the request
/// proceeds with no org context, so org-scoped queries return only data the user
/// is entitled to see (their personal data), exactly as if no header had been sent.
/// </summary>
public sealed class OrganizationAccessMiddleware
{
    public const string OrganizationHeaderName = "X-Organization-Id";

    private readonly RequestDelegate _next;
    private readonly ILogger<OrganizationAccessMiddleware> _logger;

    public OrganizationAccessMiddleware(RequestDelegate next, ILogger<OrganizationAccessMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentUserService currentUser,
        IOrganizationRepository organizationRepository)
    {
        if (currentUser.IsAuthenticated && currentUser.CurrentOrganizationId is { } orgId)
        {
            var userId = currentUser.UserId!.Value;
            var isMember = await organizationRepository.IsMemberAsync(userId, orgId, context.RequestAborted);
            if (!isMember)
            {
                _logger.LogWarning(
                    "User {UserId} sent X-Organization-Id {OrganizationId} but is not a member; stripping header for this request",
                    userId, orgId);
                context.Request.Headers.Remove(OrganizationHeaderName);
            }
        }

        await _next(context);
    }
}
