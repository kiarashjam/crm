using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Filters;

/// <summary>
/// A workspace's single sign-on requirement, for actions that name the workspace another way
/// than X-Organization-Id (its id in the route, or one of its invitations or join requests),
/// which SsoRequirementMiddleware does not see.
/// </summary>
public static class SsoGate
{
    /// <summary>
    /// The refusal when <paramref name="organizationId"/> requires single sign-on of the caller
    /// and their session began another way; null when it admits them, or they are not a member
    /// (the action's own check answers an outsider).
    /// </summary>
    public static async Task<IActionResult?> RefusalAsync(HttpContext http, Guid? organizationId, CancellationToken ct, bool knownMember = false)
    {
        if (organizationId is not Guid orgId) return null;
        var services = http.RequestServices;
        if (services.GetRequiredService<ICurrentUserService>().UserId is not Guid userId) return null;
        if (!knownMember && !await services.GetRequiredService<IOrganizationRepository>().IsMemberAsync(userId, orgId, ct)) return null;

        var workspace = await services.GetRequiredService<IOrganizationSso>().RefusalAsync(orgId, userId, SignInMethods.Of(http.User), ct);
        if (workspace == null) return null;

        var refusal = DomainErrors.Sso.Required(workspace).ToProblemResult();
        if (refusal.Value is ProblemDetails problem) problem.Extensions["organizationId"] = orgId;
        return refusal;
    }
}
