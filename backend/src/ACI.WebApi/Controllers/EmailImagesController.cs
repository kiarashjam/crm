using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.WebApi.Extensions;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>The workspace's images for its emails: upload one, list them, remove one.</summary>
[ApiController]
[Route("api/email-images")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
[Requires(MemberPermissions.EditRecords, WritesOnly = true)]
public class EmailImagesController : ControllerBase
{
    private readonly IEmailImageService _images;
    private readonly ICurrentUserService _currentUser;

    public EmailImagesController(IEmailImageService images, ICurrentUserService currentUser)
    {
        _images = images;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmailImageDto>>> List(CancellationToken ct)
    {
        if (_currentUser.UserId is null || _currentUser.CurrentOrganizationId is not { } org) return Unauthorized();
        return Ok(await _images.ListAsync(org, ct));
    }

    /// <summary>Keeps an image (multipart: file, and optionally width and height in pixels) and returns its public address.</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(EmailImageService.MaxBytes + 64 * 1024)]
    public async Task<ActionResult<EmailImageDto>> Upload(IFormFile? file, [FromForm] int? width, [FromForm] int? height, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId || _currentUser.CurrentOrganizationId is not { } org) return Unauthorized();
        if (file == null || file.Length == 0) return new ACI.Application.Common.Error("EmailImage.Empty", "Choose an image to upload.").ToProblemResult();
        if (file.Length > EmailImageService.MaxBytes)
            return new ACI.Application.Common.Error("EmailImage.TooLarge", $"An image for an email can be at most {EmailImageService.MaxBytes / (1024 * 1024)} MB.").ToProblemResult();
        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, ct);
        var result = await _images.UploadAsync(org, userId, new EmailImageUpload(file.FileName, buffer.ToArray(), width, height), ct);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : result.ToActionResult();
    }

    /// <summary>Removes an image. Emails already sent with it show it no more.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (_currentUser.UserId is null || _currentUser.CurrentOrganizationId is not { } org) return Unauthorized();
        return (await _images.DeleteAsync(id, org, ct)).ToNoContentResult();
    }
}

/// <summary>
/// An email image at its public address, for any mail app. Anonymous by design: the
/// random id is the address, and the image is one the workspace put in its emails.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/public")]
public class PublicEmailImagesController : ControllerBase
{
    private readonly IEmailImageService _images;
    public PublicEmailImagesController(IEmailImageService images) => _images = images;

    [HttpGet("email-images/{id:guid}.{ext}")]
    public async Task<IActionResult> Get(Guid id, string ext, CancellationToken ct)
    {
        var image = await _images.GetPublicAsync(id, ct);
        if (image == null || !string.Equals(ext, EmailImageService.Extension(image.ContentType), StringComparison.OrdinalIgnoreCase))
            return NotFound();
        // The bytes never change for an id, so a mail app or its proxy may keep them.
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        Response.Headers.ETag = $"\"{image.Id:N}\"";
        // Shown as the image it is, never sniffed into something else, and nothing in it runs.
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox";
        return File(image.Content, image.ContentType);
    }
}
