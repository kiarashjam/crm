using ACI.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// What the public privacy page says, as the server knows it: who runs this Cadence, who
/// answers for a workspace's data, which services the data goes to, and how long it is kept.
/// </summary>
/// <remarks>
/// The page named a placeholder company in San Francisco, under California law, and listed no
/// processor; it said the session token was never exposed. It now reads its facts from here:
/// the operator from the server's configuration, and, when it is opened from one of a
/// workspace's public forms (?form=…), that workspace's own settings. Anonymous, and it holds
/// nothing that is not printed on the page.
/// </remarks>
[ApiController]
[AllowAnonymous]
[Route("api/public/privacy")]
[Produces("application/json")]
public class PublicPrivacyController : ControllerBase
{
    private readonly IPrivacySettings _privacy;

    public PublicPrivacyController(IPrivacySettings privacy) => _privacy = privacy;

    /// <param name="form">The token of a workspace's capture or application form, to name that workspace.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    [ProducesResponseType(typeof(PrivacyNoticeDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PrivacyNoticeDto>> Get([FromQuery] string? form, CancellationToken ct) =>
        Ok(await _privacy.NoticeAsync(form, ct));
}
