using ACI.Application.Interfaces;
using ACI.Domain.Entities;

namespace ACI.Application.Common;

/// <summary>The ids a write is about to put on a record. Null means this write does not set it.</summary>
public sealed record RecordLinks
{
    public Guid? LeadId { get; init; }
    public Guid? DealId { get; init; }
    public Guid? ContactId { get; init; }
    public Guid? CompanyId { get; init; }
    public Guid? AssigneeId { get; init; }
    /// <summary>
    /// The assignee a record keeps while this write gives it <see cref="DealId"/>. Not
    /// checked as a member, since they may have left since, only for whether they can
    /// open the deal.
    /// </summary>
    public Guid? KeptAssigneeId { get; init; }
    public Guid? PipelineId { get; init; }
    public Guid? DealStageId { get; init; }
}

/// <summary>
/// The one check every write runs on the ids it was sent, before anything is saved.
/// </summary>
/// <remarks>
/// A foreign key only proves that a row exists somewhere. Tasks, deals, contacts and
/// leads copied these ids straight from the request, so a member could link a task to
/// another workspace's contact, or to a private deal they cannot open, and read its
/// name back; or assign a task to anyone with an account and have its reminders
/// emailed to them. So: a linked record has to be in the caller's workspace, by the
/// rule the repositories read by (a deal as DealVisibility shows it to the caller);
/// an assignee has to be a member, or in a personal workspace the caller; and a stage
/// has to be one of the workspace's, in the pipeline it is set with.
/// </remarks>
public static class WorkspaceLinks
{
    public static async Task<Result> CheckLinksAsync(
        this IWorkspaceRecords records, Guid userId, Guid? organizationId, RecordLinks links, CancellationToken ct = default)
    {
        if (links.LeadId is Guid leadId && !await records.ExistsAsync(RecordTypes.Lead, leadId, userId, organizationId, ct))
            return DomainErrors.Link.LeadNotFound;
        if (links.DealId is Guid dealId && !await records.ExistsAsync(RecordTypes.Deal, dealId, userId, organizationId, ct))
            return DomainErrors.Link.DealNotFound;
        if (links.ContactId is Guid contactId && !await records.ExistsAsync(RecordTypes.Contact, contactId, userId, organizationId, ct))
            return DomainErrors.Link.ContactNotFound;
        if (links.CompanyId is Guid companyId && !await records.ExistsAsync(RecordTypes.Company, companyId, userId, organizationId, ct))
            return DomainErrors.Link.CompanyNotFound;

        if (links.AssigneeId is Guid assigneeId && !await records.IsAssignableAsync(assigneeId, userId, organizationId, ct))
            return DomainErrors.Link.AssigneeNotMember;
        // Work hanging off a deal goes to someone who can open that deal. A task on a
        // private deal is hidden with the deal, and its reminders would still reach
        // an assignee who cannot see either.
        if ((links.AssigneeId ?? links.KeptAssigneeId) is Guid forWhom && links.DealId is Guid assignedDeal
            && !await records.ExistsAsync(RecordTypes.Deal, assignedDeal, forWhom, organizationId, ct))
            return DomainErrors.Link.AssigneeCannotSeeDeal;

        if (links.PipelineId is Guid pipelineId && !await records.PipelineExistsAsync(pipelineId, organizationId, ct))
            return DomainErrors.Deal.PipelineNotFound;
        if (links.DealStageId is Guid stageId)
        {
            var stagePipeline = await records.StagePipelineAsync(stageId, organizationId, ct);
            if (stagePipeline == null || (links.PipelineId is Guid chosen && chosen != stagePipeline))
                return DomainErrors.Deal.InvalidStage;
        }

        return Result.Success();
    }
}
