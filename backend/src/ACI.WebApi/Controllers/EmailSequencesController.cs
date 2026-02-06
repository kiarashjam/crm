using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Manages automated email sequences (drip campaigns).
/// </summary>
/// <remarks>
/// Email sequences allow creating multi-step email campaigns that send
/// messages automatically over time. Contacts can be enrolled in sequences
/// and will receive emails based on configured delays between steps.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class EmailSequencesController : ControllerBase
{
    private readonly IEmailSequenceService _sequenceService;
    private readonly ICurrentUserService _currentUser;

    /// <summary>
    /// Initializes a new instance of the EmailSequencesController.
    /// </summary>
    public EmailSequencesController(IEmailSequenceService sequenceService, ICurrentUserService currentUser)
    {
        _sequenceService = sequenceService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Gets all email sequences.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of email sequences.</returns>
    /// <response code="200">Returns the list of sequences.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EmailSequenceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<EmailSequenceDto>>> GetAll(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var sequences = await _sequenceService.GetSequencesAsync(userId.Value, _currentUser.OrganizationId, ct);
        return Ok(sequences);
    }

    /// <summary>
    /// Gets a specific email sequence by ID.
    /// </summary>
    /// <param name="id">The sequence ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The email sequence if found.</returns>
    /// <response code="200">Returns the sequence.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Sequence not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EmailSequenceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmailSequenceDto>> GetById(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var sequence = await _sequenceService.GetByIdAsync(userId.Value, id, ct);
        return sequence == null ? NotFound() : Ok(sequence);
    }

    /// <summary>
    /// Creates a new email sequence.
    /// </summary>
    /// <remarks>
    /// A sequence can be created empty and have steps added later,
    /// or include initial steps in the creation request.
    /// </remarks>
    /// <param name="request">Sequence creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created sequence.</returns>
    /// <response code="201">Sequence created successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPost]
    [ProducesResponseType(typeof(EmailSequenceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<EmailSequenceDto>> Create([FromBody] CreateEmailSequenceRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var created = await _sequenceService.CreateAsync(userId.Value, _currentUser.OrganizationId, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Updates an existing email sequence.
    /// </summary>
    /// <param name="id">The sequence ID to update.</param>
    /// <param name="request">Sequence update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated sequence.</returns>
    /// <response code="200">Sequence updated successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Sequence not found.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EmailSequenceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmailSequenceDto>> Update(Guid id, [FromBody] UpdateEmailSequenceRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        try
        {
            var updated = await _sequenceService.UpdateAsync(userId.Value, id, request, ct);
            return Ok(updated);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Deletes an email sequence.
    /// </summary>
    /// <remarks>
    /// Active enrollments may be affected when a sequence is deleted.
    /// </remarks>
    /// <param name="id">The sequence ID to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Sequence deleted successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        await _sequenceService.DeleteAsync(userId.Value, id, ct);
        return NoContent();
    }

    /// <summary>
    /// Adds a new step to a sequence.
    /// </summary>
    /// <remarks>
    /// Steps are executed in order based on their position.
    /// Each step has a delay before it is sent (relative to the previous step or enrollment).
    /// </remarks>
    /// <param name="id">The sequence ID.</param>
    /// <param name="request">Step creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated sequence with the new step.</returns>
    /// <response code="200">Step added successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Sequence not found.</response>
    [HttpPost("{id:guid}/steps")]
    [ProducesResponseType(typeof(EmailSequenceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmailSequenceDto>> AddStep(Guid id, [FromBody] CreateSequenceStepRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        try
        {
            var updated = await _sequenceService.AddStepAsync(userId.Value, id, request, ct);
            return Ok(updated);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Removes a step from a sequence.
    /// </summary>
    /// <param name="sequenceId">The sequence ID.</param>
    /// <param name="stepId">The step ID to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Step removed successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Sequence or step not found.</response>
    [HttpDelete("{sequenceId:guid}/steps/{stepId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RemoveStep(Guid sequenceId, Guid stepId, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        try
        {
            await _sequenceService.RemoveStepAsync(userId.Value, sequenceId, stepId, ct);
            return NoContent();
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Gets all enrollments, optionally filtered by sequence.
    /// </summary>
    /// <remarks>
    /// Enrollments track which contacts are enrolled in which sequences
    /// and their progress through the sequence steps.
    /// </remarks>
    /// <param name="sequenceId">Optional sequence ID to filter by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of enrollments.</returns>
    /// <response code="200">Returns the list of enrollments.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("enrollments")]
    [ProducesResponseType(typeof(IReadOnlyList<EmailSequenceEnrollmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<EmailSequenceEnrollmentDto>>> GetEnrollments([FromQuery] Guid? sequenceId, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var enrollments = await _sequenceService.GetEnrollmentsAsync(userId.Value, sequenceId, ct);
        return Ok(enrollments);
    }

    /// <summary>
    /// Enrolls a contact in a sequence.
    /// </summary>
    /// <remarks>
    /// The contact will start receiving emails based on the sequence steps.
    /// Enrollment can be paused and resumed.
    /// </remarks>
    /// <param name="request">Enrollment request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created enrollment.</returns>
    /// <response code="200">Contact enrolled successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPost("enrollments")]
    [ProducesResponseType(typeof(EmailSequenceEnrollmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<EmailSequenceEnrollmentDto>> Enroll([FromBody] EnrollInSequenceRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var enrollment = await _sequenceService.EnrollAsync(userId.Value, request, ct);
        return Ok(enrollment);
    }

    /// <summary>
    /// Pauses an enrollment.
    /// </summary>
    /// <remarks>
    /// While paused, no further emails will be sent.
    /// The enrollment can be resumed to continue from where it left off.
    /// </remarks>
    /// <param name="id">The enrollment ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated enrollment.</returns>
    /// <response code="200">Enrollment paused successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Enrollment not found.</response>
    [HttpPost("enrollments/{id:guid}/pause")]
    [ProducesResponseType(typeof(EmailSequenceEnrollmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmailSequenceEnrollmentDto>> PauseEnrollment(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        try
        {
            var enrollment = await _sequenceService.PauseEnrollmentAsync(userId.Value, id, ct);
            return Ok(enrollment);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Resumes a paused enrollment.
    /// </summary>
    /// <param name="id">The enrollment ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated enrollment.</returns>
    /// <response code="200">Enrollment resumed successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Enrollment not found.</response>
    [HttpPost("enrollments/{id:guid}/resume")]
    [ProducesResponseType(typeof(EmailSequenceEnrollmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmailSequenceEnrollmentDto>> ResumeEnrollment(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        try
        {
            var enrollment = await _sequenceService.ResumeEnrollmentAsync(userId.Value, id, ct);
            return Ok(enrollment);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Unenrolls a contact from a sequence.
    /// </summary>
    /// <remarks>
    /// Stops all future emails in the sequence for this contact.
    /// </remarks>
    /// <param name="id">The enrollment ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Contact unenrolled successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpDelete("enrollments/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> Unenroll(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        await _sequenceService.UnenrollAsync(userId.Value, id, ct);
        return NoContent();
    }
}
