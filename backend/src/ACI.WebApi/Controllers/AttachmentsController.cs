using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Files attached to leads, contacts, companies and deals, in the current workspace.
/// </summary>
[ApiController]
[Route("api/attachments")]
[Authorize]
[Produces("application/json")]
public class AttachmentsController : ControllerBase
{
    // Types a browser can show without running anything. Everything else downloads.
    private static readonly HashSet<string> Inline = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/gif", "image/webp", "application/pdf", "text/plain",
    };

    private readonly IAttachmentService _attachments;
    private readonly ICurrentUserService _currentUser;

    /// <summary>
    /// Initializes a new instance of the AttachmentsController.
    /// </summary>
    public AttachmentsController(IAttachmentService attachments, ICurrentUserService currentUser)
    {
        _attachments = attachments;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Lists a record's attachments, newest first. Each carries a signed download link.
    /// </summary>
    /// <param name="entityType">lead, contact, company or deal.</param>
    /// <param name="recordId">The record's id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns the attachments.</response>
    /// <response code="400">The entity type is not one that takes attachments.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AttachmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<AttachmentDto>>> List(
        [FromQuery] string? entityType, [FromQuery] Guid recordId, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        var result = await _attachments.ListAsync(userId, _currentUser.CurrentOrganizationId, entityType, recordId, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Attaches a file (multipart form: file, entityType, recordId). Up to 10 MB.
    /// </summary>
    /// <response code="200">Returns the new attachment.</response>
    /// <response code="400">No file, an empty or oversized file, or an unknown entity type.</response>
    /// <response code="404">The record is not in this workspace.</response>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(32 * 1024 * 1024)]
    [ProducesResponseType(typeof(AttachmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AttachmentDto>> Upload(
        IFormFile? file, [FromForm] string? entityType, [FromForm] Guid recordId, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        if (file == null || file.Length == 0) return DomainErrors.Attachment.Empty.ToProblemResult();
        // Refuse before reading it into memory.
        if (file.Length > AttachmentService.MaxBytes) return DomainErrors.Attachment.TooLarge.ToProblemResult();

        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, ct);
        var upload = new AttachmentUpload(entityType ?? string.Empty, recordId, file.FileName, file.ContentType, buffer.ToArray());
        var result = await _attachments.UploadAsync(userId, _currentUser.UserName, _currentUser.CurrentOrganizationId, upload, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Removes an attachment.
    /// </summary>
    /// <response code="204">Removed.</response>
    /// <response code="404">Not in this workspace.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        var result = await _attachments.DeleteAsync(userId, _currentUser.CurrentOrganizationId, id, ct);
        return result.ToNoContentResult();
    }

    /// <summary>
    /// The file behind a signed link. Anonymous by design: the client opens it as a plain
    /// link, so the token is the permission. An invalid or expired one reads as not found.
    /// </summary>
    /// <response code="200">The file.</response>
    /// <response code="404">Unknown attachment, or an invalid or expired link.</response>
    [HttpGet("{id:guid}/content/{token}")]
    [AllowAnonymous]
    [Produces("application/octet-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Content(Guid id, string token, CancellationToken ct = default)
    {
        var result = await _attachments.OpenAsync(id, token, ct);
        if (result.IsFailure) return NotFound();

        var file = result.Value;
        var inline = Inline.Contains(file.ContentType);
        var disposition = new ContentDispositionHeaderValue(inline ? "inline" : "attachment");
        disposition.SetHttpFileName(file.FileName);
        Response.Headers[HeaderNames.ContentDisposition] = disposition.ToString();
        Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        Response.Headers[HeaderNames.CacheControl] = "private, no-store";
        // Belt and braces for what does render: no script, no plugins. A PDF is left to
        // the browser's own viewer, which a sandboxed page would refuse to load.
        if (file.ContentType != "application/pdf")
            Response.Headers[HeaderNames.ContentSecurityPolicy] = "sandbox; default-src 'none'; img-src 'self'; style-src 'unsafe-inline'";
        return File(file.Content, file.ContentType);
    }
}
