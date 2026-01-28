namespace ACI.Application.Interfaces;

/// <summary>
/// Provides the current authenticated user id (from JWT). Implemented in WebApi.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    bool IsAuthenticated { get; }
}
