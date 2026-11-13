using ACI.Application.Common;
using ACI.Application.Interfaces;

namespace ACI.WebApi.Services;

/// <summary>
/// The workspace this request or job reads as (see <see cref="ITenantContext"/>). One per
/// request or job scope, so acting for an organization lasts no longer than the work it is for.
/// </summary>
public sealed class TenantContext : ActingWorkspace
{
    private readonly ICurrentUserService _currentUser;
    private readonly IHttpContextAccessor _http;

    public TenantContext(ICurrentUserService currentUser, IHttpContextAccessor http)
    {
        _currentUser = currentUser;
        _http = http;
    }

    // X-Organization-Id counts only for a signed-in caller. OrganizationAccessMiddleware
    // checks that the caller belongs to the organization, and strips the header when they
    // do not, but only for a signed-in request: on an anonymous one the header is whatever
    // the caller wrote, and reading as it would let anyone pick the organization.
    //
    // A member signed in to the member portal reads as the club their session token names,
    // which the member scheme checked against a session that still stands; the header plays no
    // part there. Only the member scheme makes that identity, so a staff token never reads so.
    protected override Guid? DefaultOrganizationId =>
        _currentUser.IsAuthenticated
            ? _currentUser.CurrentOrganizationId
            : MemberPortalClaims.Of(_http.HttpContext?.User)?.OrganizationId;

    protected override Guid? DefaultUserId => _currentUser.UserId;
}
