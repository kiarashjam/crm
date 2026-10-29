using ACI.Application.Interfaces;

namespace ACI.Application.Tests.Services;

internal static class WorkspaceRecordsMock
{
    /// <summary>
    /// Every linked record is in the workspace and everyone is a member, for tests about
    /// something other than the link check.
    /// </summary>
    public static Mock<IWorkspaceRecords> InWorkspace()
    {
        var records = new Mock<IWorkspaceRecords>();
        records.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        records.Setup(r => r.IsAssignableAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        records.Setup(r => r.PipelineExistsAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return records;
    }
}
