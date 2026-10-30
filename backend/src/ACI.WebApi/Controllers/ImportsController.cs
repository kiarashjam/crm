using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Importing spreadsheets of leads, contacts and companies: reading the file, a dry run,
/// the import itself, the list of past imports, and undoing one.
/// </summary>
/// <remarks>
/// The file travels with each request rather than being kept on the server between them,
/// so nothing is left behind by an import someone starts and abandons. A Viewer is
/// refused every POST here by the read-only middleware, the dry run included.
/// </remarks>
[ApiController]
[Route("api/imports")]
[Authorize]
[Produces("application/json")]
public class ImportsController : ControllerBase
{
    // The largest file, base64-encoded, with room for the rest of the request.
    private const long MaxRequestBytes = 8 * 1024 * 1024;

    private readonly IImportService _imports;
    private readonly ICurrentUserService _currentUser;

    /// <summary>
    /// Initializes a new instance of the ImportsController.
    /// </summary>
    public ImportsController(IImportService imports, ICurrentUserService currentUser)
    {
        _imports = imports;
        _currentUser = currentUser;
    }

    /// <summary>Reads a file: its separator, its columns with a few values each, and the fields they can go into.</summary>
    /// <response code="200">What the file holds, with a suggested field for each column.</response>
    /// <response code="400">The file cannot be read, is empty, or is too large.</response>
    [HttpPost("analyze")]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(ImportAnalysisDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportAnalysisDto>> Analyze([FromBody] ImportFileRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _imports.AnalyzeAsync(userId, _currentUser.CurrentOrganizationId, request, ct)).ToActionResult();
    }

    /// <summary>A dry run: what each row would do, with nothing written.</summary>
    /// <response code="200">Each row's outcome, as the import would have it.</response>
    /// <response code="400">The file or the column choices are not usable.</response>
    [HttpPost("preview")]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(ImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportResultDto>> Preview([FromBody] ImportRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _imports.RunAsync(userId, _currentUser.CurrentOrganizationId, request, dryRun: true, ct)).ToActionResult();
    }

    /// <summary>Imports the file, as one batch that can be undone.</summary>
    /// <response code="200">Each row's outcome, and the batch to undo it by.</response>
    /// <response code="400">The file or the column choices are not usable.</response>
    [HttpPost]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType(typeof(ImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportResultDto>> Import([FromBody] ImportRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _imports.RunAsync(userId, _currentUser.CurrentOrganizationId, request, dryRun: false, ct)).ToActionResult();
    }

    /// <summary>The workspace's latest imports, newest first, optionally of one kind ("lead", "contact" or "company").</summary>
    /// <response code="200">The imports.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ImportBatchDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ImportBatchDto>>> List([FromQuery] string? kind, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return Ok(await _imports.ListAsync(userId, _currentUser.CurrentOrganizationId, kind, ct));
    }

    /// <summary>What undoing the import would remove, and what it would keep and why. Nothing is removed.</summary>
    /// <response code="200">The records it would remove, by kind, and the ones it keeps.</response>
    /// <response code="403">Only whoever ran it, or the owner or a manager, can undo it.</response>
    /// <response code="404">No such import in this workspace.</response>
    [HttpGet("{id:guid}/undo")]
    [ProducesResponseType(typeof(ImportUndoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ImportUndoDto>> UndoPreview(Guid id, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _imports.UndoAsync(id, userId, _currentUser.CurrentOrganizationId, dryRun: true, ct)).ToActionResult();
    }

    /// <summary>
    /// Undoes the import: removes the records it created that nobody has changed or worked on
    /// since. Changes it made to records that were already here stay.
    /// </summary>
    /// <response code="200">What was removed, and what was kept and why.</response>
    /// <response code="403">Only whoever ran it, or the owner or a manager, can undo it.</response>
    /// <response code="404">No such import in this workspace.</response>
    [HttpPost("{id:guid}/undo")]
    [ProducesResponseType(typeof(ImportUndoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ImportUndoDto>> Undo(Guid id, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _imports.UndoAsync(id, userId, _currentUser.CurrentOrganizationId, dryRun: false, ct)).ToActionResult();
    }
}
