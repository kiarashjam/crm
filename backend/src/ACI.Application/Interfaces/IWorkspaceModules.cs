using ACI.Application.Common;
using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

/// <summary>
/// Which of the Sales and Club modules a workspace has on (see
/// <see cref="Domain.Common.WorkspaceModules"/>), and turning one on or off.
/// </summary>
public interface IWorkspaceModules
{
    /// <summary>
    /// Whether <paramref name="module"/> is on in the organization. Read once per request
    /// and kept for the rest of it; an organization that does not exist has both on, so a
    /// request about one is refused for that reason and not this one.
    /// </summary>
    Task<bool> IsOnAsync(Guid organizationId, string module, CancellationToken ct = default);

    /// <summary>
    /// Turns modules on or off; a field left out keeps its value. Takes managing the
    /// workspace's settings. One module always stays on, and a module whose automatic work
    /// is still switched on (billing, reminders, scheduled campaigns, the import of POS
    /// tickets; leads in outreach sequences) is not turned off until that is stopped, since
    /// nobody could see or stop it once its pages were gone.
    /// </summary>
    Task<Result<WorkspaceModulesDto>> SetAsync(Guid organizationId, Guid userId, SetWorkspaceModulesRequest request, CancellationToken ct = default);
}
