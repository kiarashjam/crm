using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.WebApi.Mcp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Read-only figures for another system that tracks an organization, such as the Atmosclear
/// business system (BMS), which links one of its projects to a Cadence organization.
///
/// It uses the organization's MCP key, the same key Claude and other MCP clients use, and sees
/// exactly what that key sees there: one organization, read only. The /mcp tools answer in
/// sentences written for an assistant; this answers the same organization in JSON, so a program
/// can show the numbers without parsing text.
/// </summary>
[ApiController]
[Route("api/integration")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class IntegrationController : ControllerBase
{
    private const int RecentActivityCount = 10;

    private readonly IOrganizationService _organizations;
    private readonly IReportingService _reporting;
    private readonly ILeadService _leads;
    private readonly ITaskService _tasks;
    private readonly IActivityService _activities;

    public IntegrationController(
        IOrganizationService organizations,
        IReportingService reporting,
        ILeadService leads,
        ITaskService tasks,
        IActivityService activities)
    {
        _organizations = organizations;
        _reporting = reporting;
        _leads = leads;
        _tasks = tasks;
        _activities = activities;
    }

    /// <summary>The organization's sales figures: leads, deals and pipeline, tasks and recent activity.</summary>
    /// <response code="200">The summary.</response>
    /// <response code="401">No key, or not an organization's MCP key.</response>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(OrganizationSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var apiKey = McpController.ApiKeyFrom(Request);
        if (string.IsNullOrEmpty(apiKey))
            return Unauthorized(new { error = "Missing API key. Send Authorization: Bearer <key> or X-Api-Key." });

        // Only an MCP key, as on /mcp: the lead webhook key lives in website forms.
        var organization = await _organizations.GetByMcpApiKeyAsync(apiKey, ct);
        if (organization == null)
            return Unauthorized(new { error = "Invalid API key." });

        // Read as the owner, exactly as the MCP tools read this organization.
        var owner = organization.OwnerUserId;
        var deals = await _reporting.GetDashboardStatsAsync(owner, organization.Id, ct);
        var stages = await _reporting.GetPipelineValueByStageAsync(owner, organization.Id, ct);
        var leads = await _leads.GetStatsAsync(owner, organization.Id, ct);
        var tasks = await _tasks.GetStatsAsync(owner, organization.Id, ct);
        var recent = await _activities.GetPagedAsync(owner, organization.Id, 1, RecentActivityCount, null, null, ct);

        Response.Headers.CacheControl = "no-store";
        return Ok(new OrganizationSummaryDto(
            organization.Id,
            organization.Name,
            DateTime.UtcNow,
            leads,
            tasks,
            deals,
            stages,
            recent.Items
                .OrderByDescending(a => a.CreatedAtUtc)
                .Select(a => new OrganizationActivityDto(
                    a.Type,
                    a.Subject,
                    a.DealName ?? a.ContactName ?? a.LeadName,
                    a.CreatedByName,
                    a.CreatedAtUtc))
                .ToList()));
    }
}

/// <summary>One organization's figures, for systems linked to it with its MCP key.</summary>
public sealed record OrganizationSummaryDto(
    Guid OrganizationId,
    string OrganizationName,
    DateTime GeneratedAtUtc,
    LeadStatsDto Leads,
    TaskStatsDto Tasks,
    DashboardStatsDto Deals,
    IReadOnlyList<PipelineStageValueDto> PipelineByStage,
    IReadOnlyList<OrganizationActivityDto> RecentActivity);

/// <summary>An activity without its body: what happened, about what, by whom, when.</summary>
public sealed record OrganizationActivityDto(
    string Type,
    string? Subject,
    string? About,
    string? By,
    DateTime AtUtc);
