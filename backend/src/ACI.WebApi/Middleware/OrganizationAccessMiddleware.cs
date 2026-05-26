using ACI.Application.Interfaces;

namespace ACI.WebApi.Middleware;

/// <summary>
/// Verifies that the authenticated user is a member of the organization indicated
/// by the X-Organization-Id header. Without this check, any authenticated user
/// could read or modify data in any organization by setting the header.
/// </summary>
public sealed class OrganizationAccessMiddleware
{
    private readonly RequestDelegate _next;

    public OrganizationAccessMiddleware(RequestDelegate next) => _next = next;

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
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Not a member of the requested organization",
                });
                return;
            }
        }

        await _next(context);
    }
}
