using System.Security.Claims;
using ACI.Application.Interfaces;

namespace ACI.WebApi.Services;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var idClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(idClaim, out var id) ? id : null;
        }
    }

    public Guid? CurrentOrganizationId
    {
        get
        {
            var header = _httpContextAccessor.HttpContext?.Request?.Headers["X-Organization-Id"].FirstOrDefault();
            return Guid.TryParse(header, out var id) ? id : null;
        }
    }

    public bool IsAuthenticated => UserId.HasValue;
}
