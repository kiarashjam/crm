using System.ComponentModel.DataAnnotations;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Manages tasks in the CRM system.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;
    private readonly ICurrentUserService _currentUser;

    /// <summary>
    /// Initializes a new instance of the TasksController.
    /// </summary>
    public TasksController(ITaskService taskService, ICurrentUserService currentUser)
    {
        _taskService = taskService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Retrieves paginated tasks with optional filters and search.
    /// </summary>
    /// <param name="page">Page number (1-based). Defaults to 1.</param>
    /// <param name="pageSize">Number of items per page. Defaults to 20, max 100.</param>
    /// <param name="search">Optional search term to filter by title, description, or notes.</param>
    /// <param name="overdueOnly">Filter to only overdue tasks.</param>
    /// <param name="status">Filter by status: todo, in_progress, completed, cancelled.</param>
    /// <param name="priority">Filter by priority: none, low, medium, high.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paginated list of tasks matching the filters.</returns>
    /// <response code="200">Returns the paginated list of tasks.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TaskDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<TaskDto>>> GetTasks(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? overdueOnly = null, 
        [FromQuery] string? status = null,
        [FromQuery] string? priority = null,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        
        var filters = new TaskFilterParams(
            OverdueOnly: overdueOnly,
            Status: status,
            Priority: priority
        );
        
        var result = await _taskService.GetPagedAsync(
            userId.Value, 
            _currentUser.CurrentOrganizationId,
            page,
            pageSize,
            search,
            filters, 
            ct);
        
        return Ok(result);
    }

    /// <summary>
    /// Retrieves all tasks with optional filters (non-paginated).
    /// </summary>
    /// <param name="overdueOnly">Filter to only overdue tasks.</param>
    /// <param name="status">Filter by status: todo, in_progress, completed, cancelled.</param>
    /// <param name="priority">Filter by priority: none, low, medium, high.</param>
    /// <param name="assigneeId">Filter by assignee ID.</param>
    /// <param name="leadId">Filter by linked lead ID.</param>
    /// <param name="dealId">Filter by linked deal ID.</param>
    /// <param name="contactId">Filter by linked contact ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of tasks matching the filters.</returns>
    /// <response code="200">Returns the list of tasks.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("all")]
    [ProducesResponseType(typeof(IReadOnlyList<TaskDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<TaskDto>>> GetAllTasks(
        [FromQuery] bool? overdueOnly, 
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] string? assigneeId,
        [FromQuery] string? leadId,
        [FromQuery] string? dealId,
        [FromQuery] string? contactId,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var filters = new TaskFilterParams(
            OverdueOnly: overdueOnly,
            Status: status,
            Priority: priority,
            AssigneeId: assigneeId,
            LeadId: leadId,
            DealId: dealId,
            ContactId: contactId
        );
        
        var list = await _taskService.GetTasksAsync(
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            filters, 
            ct);
        
        return Ok(list);
    }

    /// <summary>
    /// Retrieves all tasks linked to a specific lead.
    /// </summary>
    /// <param name="leadId">The lead ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of tasks linked to the lead.</returns>
    /// <response code="200">Returns the list of tasks.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("by-lead/{leadId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<TaskDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<TaskDto>>> GetTasksByLead(
        Guid leadId, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var list = await _taskService.GetTasksByLeadIdAsync(
            leadId, 
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            ct);
        
        return Ok(list);
    }

    /// <summary>
    /// Retrieves all tasks linked to a specific deal.
    /// </summary>
    /// <param name="dealId">The deal ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of tasks linked to the deal.</returns>
    /// <response code="200">Returns the list of tasks.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("by-deal/{dealId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<TaskDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<TaskDto>>> GetTasksByDeal(
        Guid dealId, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var list = await _taskService.GetTasksByDealIdAsync(
            dealId, 
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            ct);
        
        return Ok(list);
    }

    /// <summary>
    /// Retrieves all tasks linked to a specific contact.
    /// </summary>
    /// <param name="contactId">The contact ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of tasks linked to the contact.</returns>
    /// <response code="200">Returns the list of tasks.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("by-contact/{contactId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<TaskDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<TaskDto>>> GetTasksByContact(
        Guid contactId, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var list = await _taskService.GetTasksByContactIdAsync(
            contactId, 
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            ct);
        
        return Ok(list);
    }

    /// <summary>
    /// Retrieves a specific task by ID.
    /// </summary>
    /// <param name="id">The task ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The task if found.</returns>
    /// <response code="200">Returns the task.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Task not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskDto>> GetById(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _taskService.GetByIdAsync(
            id, 
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            ct);
        
        return result.ToActionResult();
    }

    /// <summary>
    /// Retrieves task statistics for the dashboard.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Task statistics.</returns>
    /// <response code="200">Returns the task statistics.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(TaskStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TaskStatsDto>> GetStats(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var stats = await _taskService.GetStatsAsync(
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            ct);
        
        return Ok(stats);
    }

    /// <summary>
    /// Creates a new task.
    /// </summary>
    /// <param name="request">The task creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created task.</returns>
    /// <response code="200">Task created successfully.</response>
    /// <response code="400">Invalid request data.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPost]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TaskDto>> Create(
        [FromBody] CreateTaskRequest request, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _taskService.CreateAsync(
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            request, 
            ct);
        
        return result.ToActionResult();
    }

    /// <summary>
    /// Updates an existing task.
    /// </summary>
    /// <param name="id">The task ID to update.</param>
    /// <param name="request">The task update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated task.</returns>
    /// <response code="200">Task updated successfully.</response>
    /// <response code="400">Invalid request data.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Task not found.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskDto>> Update(
        Guid id, 
        [FromBody] UpdateTaskRequest request, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _taskService.UpdateAsync(
            id, 
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            request, 
            ct);
        
        return result.ToActionResult();
    }

    /// <summary>
    /// Updates the status of a task.
    /// </summary>
    /// <param name="id">The task ID.</param>
    /// <param name="request">The status update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated task.</returns>
    /// <response code="200">Task status updated successfully.</response>
    /// <response code="400">Invalid status value.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Task not found.</response>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskDto>> UpdateStatus(
        Guid id, 
        [FromBody] UpdateStatusRequest request, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _taskService.UpdateStatusAsync(
            id, 
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            request.Status, 
            ct);
        
        return result.ToActionResult();
    }

    /// <summary>
    /// Assigns or unassigns a task to a user.
    /// </summary>
    /// <param name="id">The task ID.</param>
    /// <param name="request">The assignment request. Set AssigneeId to null to unassign.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated task.</returns>
    /// <response code="200">Task assigned successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Task not found.</response>
    [HttpPatch("{id:guid}/assign")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskDto>> Assign(
        Guid id, 
        [FromBody] AssignTaskRequest request, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _taskService.AssignTaskAsync(
            id, 
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            request.AssigneeId, 
            ct);
        
        return result.ToActionResult();
    }

    /// <summary>
    /// Links or unlinks a task to a lead.
    /// </summary>
    /// <param name="id">The task ID.</param>
    /// <param name="request">The lead link request. Set LeadId to null to unlink.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated task.</returns>
    /// <response code="200">Task linked to lead successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Task not found.</response>
    [HttpPatch("{id:guid}/link-lead")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskDto>> LinkToLead(
        Guid id, 
        [FromBody] LinkToLeadRequest request, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _taskService.LinkToLeadAsync(
            id, 
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            request.LeadId, 
            ct);
        
        return result.ToActionResult();
    }

    /// <summary>
    /// Links or unlinks a task to a deal.
    /// </summary>
    /// <param name="id">The task ID.</param>
    /// <param name="request">The deal link request. Set DealId to null to unlink.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated task.</returns>
    /// <response code="200">Task linked to deal successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Task not found.</response>
    [HttpPatch("{id:guid}/link-deal")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskDto>> LinkToDeal(
        Guid id, 
        [FromBody] LinkToDealRequest request, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _taskService.LinkToDealAsync(
            id, 
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            request.DealId, 
            ct);
        
        return result.ToActionResult();
    }

    /// <summary>
    /// Deletes a task.
    /// </summary>
    /// <param name="id">The task ID to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Task deleted successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Task not found.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _taskService.DeleteAsync(
            id, 
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            ct);
        
        return result.ToNoContentResult();
    }
}

/// <summary>
/// Request to update task status.
/// </summary>
public record UpdateStatusRequest
{
    /// <summary>
    /// The new status: todo, in_progress, completed, cancelled.
    /// </summary>
    [Required(ErrorMessage = "Status is required")]
    [RegularExpression("^(todo|in_progress|completed|cancelled)$", ErrorMessage = "Status must be one of: todo, in_progress, completed, cancelled")]
    public required string Status { get; init; }
}

/// <summary>
/// Request to assign a task.
/// </summary>
public record AssignTaskRequest
{
    /// <summary>
    /// The user ID to assign the task to, or null to unassign.
    /// </summary>
    public Guid? AssigneeId { get; init; }
}

/// <summary>
/// Request to link a task to a lead.
/// </summary>
public record LinkToLeadRequest
{
    /// <summary>
    /// The lead ID to link to, or null to unlink.
    /// </summary>
    public Guid? LeadId { get; init; }
}

/// <summary>
/// Request to link a task to a deal.
/// </summary>
public record LinkToDealRequest
{
    /// <summary>
    /// The deal ID to link to, or null to unlink.
    /// </summary>
    public Guid? DealId { get; init; }
}
