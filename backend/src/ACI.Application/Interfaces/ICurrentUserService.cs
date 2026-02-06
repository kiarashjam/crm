namespace ACI.Application.Interfaces;

/// <summary>
/// Provides the current authenticated user id (from JWT) and optional organization id (from X-Organization-Id header). Implemented in WebApi.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    /// <summary>From X-Organization-Id header; null if not sent. Caller must validate user is member of org when using.</summary>
    Guid? CurrentOrganizationId { get; }
    /// <summary>Alias for CurrentOrganizationId for convenience.</summary>
    Guid? OrganizationId => CurrentOrganizationId;
    bool IsAuthenticated { get; }
}
