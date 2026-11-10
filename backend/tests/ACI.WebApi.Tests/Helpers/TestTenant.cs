using ACI.Application.Interfaces;

namespace ACI.WebApi.Tests.Helpers;

/// <summary>A workspace to read as, for a context or service a test builds itself.</summary>
public sealed class TestTenant : ITenantContext
{
    public TestTenant(Guid? organizationId = null, Guid? userId = null)
    {
        OrganizationId = organizationId;
        UserId = userId;
    }

    public Guid? OrganizationId { get; private set; }
    public Guid? UserId { get; private set; }

    public IDisposable ActFor(Guid? organizationId, Guid? userId)
    {
        var (previousOrganization, previousUser) = (OrganizationId, UserId);
        (OrganizationId, UserId) = (organizationId, userId);
        return new Restore(() => (OrganizationId, UserId) = (previousOrganization, previousUser));
    }

    private sealed class Restore(Action undo) : IDisposable
    {
        public void Dispose() => undo();
    }
}
