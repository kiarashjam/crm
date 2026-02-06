using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Manages user settings and preferences in the CRM system.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settingsService;
    private readonly ICurrentUserService _currentUser;

    /// <summary>
    /// Initializes a new instance of the SettingsController.
    /// </summary>
    public SettingsController(ISettingsService settingsService, ICurrentUserService currentUser)
    {
        _settingsService = settingsService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Retrieves the current user's settings.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The user's settings.</returns>
    /// <response code="200">Returns the user's settings.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(UserSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserSettingsDto>> Get(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var settings = await _settingsService.GetAsync(userId.Value, ct);
        return Ok(settings);
    }

    /// <summary>
    /// Updates the current user's settings.
    /// </summary>
    /// <param name="request">The settings update request. Only provided fields are updated.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated settings.</returns>
    /// <remarks>
    /// This is a partial update - only the fields you provide will be changed.
    /// Fields not included in the request will retain their current values.
    /// </remarks>
    /// <response code="200">Settings updated successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPut]
    [ProducesResponseType(typeof(UserSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserSettingsDto>> Save(
        [FromBody] UpdateUserSettingsRequest request, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var settings = await _settingsService.SaveAsync(userId.Value, request, ct);
        return Ok(settings);
    }

    /// <summary>
    /// Updates a specific section of the user's settings.
    /// </summary>
    /// <param name="section">The settings section to update: profile, brand, appearance, notifications, defaults, privacy.</param>
    /// <param name="request">The settings update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated settings.</returns>
    /// <response code="200">Settings section updated successfully.</response>
    /// <response code="400">Invalid section name.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPatch("{section}")]
    [ProducesResponseType(typeof(UserSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserSettingsDto>> UpdateSection(
        string section, 
        [FromBody] UpdateUserSettingsRequest request, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();

        // Validate section name (for API clarity, actual update is same)
        var validSections = new[] { "profile", "brand", "appearance", "notifications", "defaults", "privacy" };
        if (!validSections.Contains(section.ToLowerInvariant()))
        {
            return Problem(
                detail: $"Invalid section. Valid sections: {string.Join(", ", validSections)}",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request"
            );
        }

        var settings = await _settingsService.SaveAsync(userId.Value, request, ct);
        return Ok(settings);
    }

    /// <summary>
    /// Resets all user settings to their default values.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The reset settings with default values.</returns>
    /// <response code="200">Settings reset to defaults successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPost("reset")]
    [ProducesResponseType(typeof(UserSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserSettingsDto>> ResetToDefaults(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var settings = await _settingsService.ResetToDefaultsAsync(userId.Value, ct);
        return Ok(settings);
    }

    /// <summary>
    /// Retrieves the list of available timezones.
    /// </summary>
    /// <returns>A list of timezones with their IDs, display names, and UTC offsets.</returns>
    /// <response code="200">Returns the list of timezones.</response>
    [HttpGet("timezones")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<TimezoneInfo>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<TimezoneInfo>> GetTimezones()
    {
        var timezones = TimeZoneInfo.GetSystemTimeZones()
            .Select(tz => new TimezoneInfo(tz.Id, tz.DisplayName, tz.BaseUtcOffset.TotalHours))
            .OrderBy(t => t.UtcOffset)
            .ToList();
        return Ok(timezones);
    }

    /// <summary>
    /// Retrieves the list of available currencies.
    /// </summary>
    /// <returns>A list of currencies with their codes, names, and symbols.</returns>
    /// <response code="200">Returns the list of currencies.</response>
    [HttpGet("currencies")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<CurrencyInfo>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<CurrencyInfo>> GetCurrencies()
    {
        var currencies = new List<CurrencyInfo>
        {
            new("USD", "US Dollar", "$"),
            new("EUR", "Euro", "€"),
            new("GBP", "British Pound", "£"),
            new("JPY", "Japanese Yen", "¥"),
            new("AUD", "Australian Dollar", "A$"),
            new("CAD", "Canadian Dollar", "C$"),
            new("CHF", "Swiss Franc", "CHF"),
            new("CNY", "Chinese Yuan", "¥"),
            new("INR", "Indian Rupee", "₹"),
            new("MXN", "Mexican Peso", "MX$"),
            new("BRL", "Brazilian Real", "R$"),
            new("KRW", "South Korean Won", "₩"),
            new("SEK", "Swedish Krona", "kr"),
            new("NOK", "Norwegian Krone", "kr"),
            new("DKK", "Danish Krone", "kr"),
            new("NZD", "New Zealand Dollar", "NZ$"),
            new("SGD", "Singapore Dollar", "S$"),
            new("HKD", "Hong Kong Dollar", "HK$"),
            new("AED", "UAE Dirham", "د.إ"),
            new("SAR", "Saudi Riyal", "﷼"),
        };
        return Ok(currencies);
    }

    /// <summary>
    /// Retrieves the list of available languages.
    /// </summary>
    /// <returns>A list of languages with their codes and names.</returns>
    /// <response code="200">Returns the list of languages.</response>
    [HttpGet("languages")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<LanguageInfo>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<LanguageInfo>> GetLanguages()
    {
        var languages = new List<LanguageInfo>
        {
            new("en", "English", "English"),
            new("es", "Spanish", "Español"),
            new("fr", "French", "Français"),
            new("de", "German", "Deutsch"),
            new("it", "Italian", "Italiano"),
            new("pt", "Portuguese", "Português"),
            new("nl", "Dutch", "Nederlands"),
            new("ja", "Japanese", "日本語"),
            new("ko", "Korean", "한국어"),
            new("zh", "Chinese", "中文"),
            new("ar", "Arabic", "العربية"),
            new("hi", "Hindi", "हिन्दी"),
            new("ru", "Russian", "Русский"),
            new("tr", "Turkish", "Türkçe"),
            new("pl", "Polish", "Polski"),
            new("sv", "Swedish", "Svenska"),
        };
        return Ok(languages);
    }
}

/// <summary>
/// Timezone information.
/// </summary>
/// <param name="Id">The timezone identifier.</param>
/// <param name="DisplayName">The display name of the timezone.</param>
/// <param name="UtcOffset">The UTC offset in hours.</param>
public record TimezoneInfo(string Id, string DisplayName, double UtcOffset);

/// <summary>
/// Currency information.
/// </summary>
/// <param name="Code">The ISO currency code.</param>
/// <param name="Name">The full name of the currency.</param>
/// <param name="Symbol">The currency symbol.</param>
public record CurrencyInfo(string Code, string Name, string Symbol);

/// <summary>
/// Language information.
/// </summary>
/// <param name="Code">The ISO language code.</param>
/// <param name="Name">The English name of the language.</param>
/// <param name="NativeName">The native name of the language.</param>
public record LanguageInfo(string Code, string Name, string NativeName);
