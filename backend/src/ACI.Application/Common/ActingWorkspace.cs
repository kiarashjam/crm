using ACI.Application.Interfaces;

namespace ACI.Application.Common;

/// <summary>
/// An <see cref="ITenantContext"/> that starts in no workspace, or in the one a subclass
/// names, and acts for another while a scope from <see cref="ActFor"/> lasts.
/// </summary>
/// <remarks>
/// A database context made without one from DI (a tool, a test) gets one of these, so it
/// reads no workspace's records until something acts for one. Not thread-safe, like the
/// database context it belongs to: one per request or job scope.
/// </remarks>
public class ActingWorkspace : ITenantContext
{
    private Workspace? _actingFor;

    /// <summary>The organization read as when nothing acts for another.</summary>
    protected virtual Guid? DefaultOrganizationId => null;

    /// <summary>Whose personal workspace is read as when nothing acts for another and there is no organization.</summary>
    protected virtual Guid? DefaultUserId => null;

    public Guid? OrganizationId => _actingFor is { } acting ? acting.OrganizationId : DefaultOrganizationId;

    public Guid? UserId => _actingFor is { } acting ? acting.UserId : DefaultUserId;

    public IDisposable ActFor(Guid? organizationId, Guid? userId)
    {
        var previous = _actingFor;
        _actingFor = new Workspace(organizationId, userId);
        return new Restore(this, previous);
    }

    private sealed record Workspace(Guid? OrganizationId, Guid? UserId);

    private sealed class Restore : IDisposable
    {
        private readonly ActingWorkspace _owner;
        private readonly Workspace? _previous;
        private bool _done;

        public Restore(ActingWorkspace owner, Workspace? previous)
        {
            _owner = owner;
            _previous = previous;
        }

        public void Dispose()
        {
            if (_done) return;
            _done = true;
            _owner._actingFor = _previous;
        }
    }
}
