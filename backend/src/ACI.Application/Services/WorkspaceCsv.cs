using ACI.Application.Common;
using ACI.Application.Interfaces;

namespace ACI.Application.Services;

/// <summary>How a workspace's CSV exports are laid out.</summary>
public interface IWorkspaceCsv
{
    /// <summary>
    /// The column separator its exports use: a semicolon when the workspace chose it (Excel set
    /// up for Switzerland splits a CSV on semicolons), else a comma, as exports always were.
    /// </summary>
    Task<char> SeparatorAsync(Guid? organizationId, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class WorkspaceCsv : IWorkspaceCsv
{
    private readonly IOrganizationRepository _organizations;

    public WorkspaceCsv(IOrganizationRepository organizations) => _organizations = organizations;

    /// <inheritdoc />
    public async Task<char> SeparatorAsync(Guid? organizationId, CancellationToken ct = default)
    {
        if (organizationId is not Guid id) return CsvExport.Comma;
        var organization = await _organizations.GetByIdAsync(id, ct);
        return CsvExport.SeparatorFor(organization?.CsvSemicolon == true);
    }
}
