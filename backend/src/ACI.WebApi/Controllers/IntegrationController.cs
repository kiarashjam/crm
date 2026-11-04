using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.WebApi.Mcp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Read-only figures and deals for another system that tracks an organization, such as the
/// Atmosclear business system (BMS), which links one of its projects to a Cadence organization, or
/// BonApp's admin, which links each restaurant to its deal.
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
    private const int DefaultDealCount = 20;
    private const int MaxDealCount = 50;

    private readonly IOrganizationService _organizations;
    private readonly IReportingService _reporting;
    private readonly ILeadService _leads;
    private readonly ITaskService _tasks;
    private readonly IActivityService _activities;
    private readonly IDealService _deals;

    public IntegrationController(
        IOrganizationService organizations,
        IReportingService reporting,
        ILeadService leads,
        ITaskService tasks,
        IActivityService activities,
        IDealService deals)
    {
        _organizations = organizations;
        _reporting = reporting;
        _leads = leads;
        _tasks = tasks;
        _activities = activities;
        _deals = deals;
    }

    /// <summary>The organization's sales figures: leads, deals and pipeline, tasks and recent activity.</summary>
    /// <response code="200">The summary.</response>
    /// <response code="401">No key, or not an organization's MCP key.</response>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(OrganizationSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var (organization, refusal) = await OrganizationFromKeyAsync(ct);
        if (organization == null)
            return refusal!;

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
                .Select(ToActivity)
                .ToList()));
    }

    /// <summary>
    /// The organization's deals, by name, open and closed. <paramref name="search"/> matches the deal,
    /// its company, its contact or its stage, as the Deals list does.
    /// </summary>
    /// <param name="search">Optional text to match.</param>
    /// <param name="take">How many deals, 1 to 50. Default 20.</param>
    /// <param name="ct">Request cancellation.</param>
    /// <response code="200">The deals, and how many match in all.</response>
    /// <response code="401">No key, or not an organization's MCP key.</response>
    [HttpGet("deals")]
    [ProducesResponseType(typeof(IntegrationDealListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Deals([FromQuery] string? search, [FromQuery] int? take, CancellationToken ct)
    {
        var (organization, refusal) = await OrganizationFromKeyAsync(ct);
        if (organization == null)
            return refusal!;

        var page = await _deals.GetDealsPagedAsync(
            organization.OwnerUserId,
            organization.Id,
            1,
            Math.Clamp(take ?? DefaultDealCount, 1, MaxDealCount),
            string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            null,
            null,
            ct);

        Response.Headers.CacheControl = "no-store";
        return Ok(new IntegrationDealListDto(
            organization.Id,
            page.TotalCount,
            page.Items.Select(ToDeal).ToList()));
    }

    /// <summary>One deal of the organization, with its latest activity and its open tasks.</summary>
    /// <param name="id">The deal's id.</param>
    /// <param name="ct">Request cancellation.</param>
    /// <response code="200">The deal.</response>
    /// <response code="401">No key, or not an organization's MCP key.</response>
    /// <response code="404">No such deal in this organization.</response>
    [HttpGet("deals/{id:guid}")]
    [ProducesResponseType(typeof(IntegrationDealDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DealById(Guid id, CancellationToken ct)
    {
        var (organization, refusal) = await OrganizationFromKeyAsync(ct);
        if (organization == null)
            return refusal!;

        var owner = organization.OwnerUserId;
        var deal = await _deals.GetByIdAsync(id, owner, organization.Id, ct);
        if (deal.IsFailure)
            return NotFound(new { error = "There is no such deal in this organization." });

        var activities = await _activities.GetByDealIdAsync(id, owner, organization.Id, ct);
        var tasks = await _tasks.GetTasksByDealIdAsync(id, owner, organization.Id, ct);

        Response.Headers.CacheControl = "no-store";
        return Ok(new IntegrationDealDetailDto(
            ToDeal(deal.Value),
            activities
                .OrderByDescending(a => a.CreatedAtUtc)
                .Take(RecentActivityCount)
                .Select(ToActivity)
                .ToList(),
            tasks
                .Where(t => t.Status is "todo" or "in_progress")
                .OrderBy(t => t.DueDateUtc ?? DateTime.MaxValue)
                .Select(t => new IntegrationTaskDto(t.Title, t.Status, t.Priority, t.DueDateUtc, t.AssigneeName))
                .ToList()));
    }

    /// <summary>The organization whose MCP key the request carries, or the 401 to answer.</summary>
    private async Task<(Organization? Organization, IActionResult? Refusal)> OrganizationFromKeyAsync(CancellationToken ct)
    {
        var apiKey = McpController.ApiKeyFrom(Request);
        if (string.IsNullOrEmpty(apiKey))
            return (null, Unauthorized(new { error = "Missing API key. Send Authorization: Bearer <key> or X-Api-Key." }));

        // Only an MCP key, as on /mcp: the lead webhook key lives in website forms.
        var organization = await _organizations.GetByMcpApiKeyAsync(apiKey, ct);
        return organization == null
            ? (null, Unauthorized(new { error = "Invalid API key." }))
            : (organization, null);
    }

    private static OrganizationActivityDto ToActivity(ActivityDto a) =>
        new(a.Type, a.Subject, a.DealName ?? a.ContactName ?? a.LeadName, a.CreatedByName, a.CreatedAtUtc);

    private static IntegrationDealDto ToDeal(DealDto d) => new(
        d.Id,
        d.Name,
        d.DealStageName ?? d.Stage,
        d.PipelineName,
        MoneyText.TryParseAmount(d.Value, out var amount) ? amount : null,
        d.Currency,
        d.CompanyName,
        d.ContactName,
        d.AssigneeName,
        d.Probability,
        d.ExpectedCloseDateUtc,
        d.IsWon switch { true => "won", false => "lost", null => "open" },
        d.ClosedAtUtc,
        d.LastActivityAtUtc,
        d.CreatedAtUtc,
        d.UpdatedAtUtc);
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

/// <summary>Some of an organization's deals, and how many match in all.</summary>
public sealed record IntegrationDealListDto(
    Guid OrganizationId,
    int TotalCount,
    IReadOnlyList<IntegrationDealDto> Items);

/// <summary>
/// A deal, for a system that links its own records to it. <c>Value</c> is null when the recorded
/// value holds no readable number. <c>Status</c> is open, won or lost.
/// </summary>
public sealed record IntegrationDealDto(
    Guid Id,
    string Name,
    string? Stage,
    string? PipelineName,
    decimal? Value,
    string? Currency,
    string? CompanyName,
    string? ContactName,
    string? AssigneeName,
    int? Probability,
    DateTime? ExpectedCloseDateUtc,
    string Status,
    DateTime? ClosedAtUtc,
    DateTime? LastActivityAtUtc,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>One deal with its latest activity (without bodies) and its open tasks.</summary>
public sealed record IntegrationDealDetailDto(
    IntegrationDealDto Deal,
    IReadOnlyList<OrganizationActivityDto> RecentActivity,
    IReadOnlyList<IntegrationTaskDto> OpenTasks);

/// <summary>An open task without its description or notes.</summary>
public sealed record IntegrationTaskDto(
    string Title,
    string Status,
    string Priority,
    DateTime? DueDateUtc,
    string? AssigneeName);
