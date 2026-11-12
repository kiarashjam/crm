using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using ACI.Application.Common;
using ACI.Application.Configuration;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ACI.Infrastructure.Services.Pos;

/// <inheritdoc cref="IClubPos"/>
/// <remarks>
/// This part holds the connection: the settings, connecting the club's Lightspeed account
/// (OAuth, its tokens kept encrypted with Data Protection and never answered back), the
/// locations, and the access token each call needs. The import, the tickets and members'
/// codes are in the other parts.
/// </remarks>
public sealed partial class ClubPosService : IClubPos
{
    /// <summary>What a POS payment method can mean, set in the settings: the ways of settling a sale, or not a member's sale at all.</summary>
    public const string Skip = "Skip";

    public static readonly string[] Means = [SaleTenders.HouseAccount, SaleTenders.Card, SaleTenders.Cash, SaleTenders.Twint, Skip];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _db;
    private readonly IClubLedger _ledger;
    private readonly ISecretProtector _secrets;
    private readonly IEnumerable<IPosProvider> _providers;
    private readonly ICurrentUserService _user;
    private readonly PosConnectState _state;
    private readonly EmailSettings _email;
    private readonly ILogger<ClubPosService> _logger;

    public ClubPosService(AppDbContext db, IClubLedger ledger, ISecretProtector secrets, IEnumerable<IPosProvider> providers,
        ICurrentUserService user, PosConnectState state, IOptions<EmailSettings> email, ILogger<ClubPosService> logger)
    {
        _db = db;
        _ledger = ledger;
        _secrets = secrets;
        _providers = providers;
        _user = user;
        _state = state;
        _email = email.Value;
        _logger = logger;
    }

    /// <summary>
    /// Where Lightspeed sends the browser back after the club connects: the POS tickets page,
    /// which passes the code on while signed in. Registered with Lightspeed for Cadence's client.
    /// </summary>
    public string RedirectUri => $"{(_email.FrontendBaseUrl ?? "").TrimEnd('/')}/pos-tickets";

    // ── status and settings ──────────────────────────────────────────────────

    public async Task<PosStatusDto> StatusAsync(Guid organizationId, CancellationToken ct = default)
    {
        var row = await RowAsync(organizationId, ct);
        var attention = await _db.ClubPosTickets.CountAsync(t => t.OrganizationId == organizationId
            && (t.Status == PosTicketStatuses.Unmatched || t.Status == PosTicketStatuses.Failed), ct);
        var connected = row?.RefreshTokenProtected != null;
        return new PosStatusDto(row?.Provider ?? PosProviders.LightspeedK, Provider(row?.Provider).IsConfigured, connected,
            connected && row!.Enabled && row.LocationId != null, row?.LocationName ?? row?.LocationId, row?.LastImportAtUtc, row?.LastImportError, attention);
    }

    public async Task<PosSettingsDto> SettingsAsync(Guid organizationId, CancellationToken ct = default) =>
        ToDto(await RowAsync(organizationId, ct));

    public async Task<Result<PosSettingsDto>> SaveSettingsAsync(Guid organizationId, Guid userId, PosSettingsRequest request, Guid? expectedVersion, CancellationToken ct = default)
    {
        var row = await RowAsync(organizationId, ct);
        if (row != null && (expectedVersion ?? request.Version) is { } version && version != row.Version) return DomainErrors.Pos.SettingsChanged;

        string? locationId = row?.LocationId;
        if (request.LocationId != null)
        {
            locationId = string.IsNullOrWhiteSpace(request.LocationId) ? null : request.LocationId.Trim();
            if (locationId != null && (locationId.Length > 100 || !Regex.IsMatch(locationId, "^[A-Za-z0-9._:-]+$")))
                return DomainErrors.Pos.SettingsInvalid("The location is Lightspeed's business location id: its digits, as Lightspeed's back office shows it.");
        }
        var locationName = row?.LocationName;
        if (request.LocationName != null) locationName = string.IsNullOrWhiteSpace(request.LocationName) ? null : Clip(request.LocationName.Trim(), 200);
        if (request.LocationId != null && request.LocationName == null && locationId != row?.LocationId) locationName = null;

        var importFrom = row?.ImportFromUtc;
        if (request.ImportFromUtc != null)
        {
            if (string.IsNullOrWhiteSpace(request.ImportFromUtc)) importFrom = null;
            else if (!DateTime.TryParse(request.ImportFromUtc, System.Globalization.CultureInfo.InvariantCulture,
                         System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var from)
                     || from > DateTime.UtcNow || from < DateTime.UtcNow.AddDays(-92))
                return DomainErrors.Pos.SettingsInvalid("Import tickets from a date in the last three months, and not in the future.");
            else importFrom = DateTime.SpecifyKind(from, DateTimeKind.Utc);
        }

        Dictionary<string, string>? map = null;
        if (request.MethodMap != null)
        {
            if (request.MethodMap.Count > 50) return DomainErrors.Pos.SettingsInvalid("List at most 50 of the POS's payment methods.");
            map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (method, means) in request.MethodMap)
            {
                var key = (method ?? "").Trim();
                if (key.Length is 0 or > 60) return DomainErrors.Pos.SettingsInvalid("A payment method is named by its code or name in the POS, at most 60 characters.");
                var value = Means.FirstOrDefault(m => string.Equals(m, means?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (value == null) return DomainErrors.Pos.SettingsInvalid($"'{key}' must mean HouseAccount, Card, Cash, Twint or Skip.");
                map[key] = value;
            }
        }

        var venue = row?.DefaultVenue ?? "Dining";
        if (request.DefaultVenue != null)
        {
            venue = request.DefaultVenue.Trim();
            if (!ClubLedgerService.TabVenues.Contains(venue)) return DomainErrors.Pos.SettingsInvalid("The POS's sales are counted at the Bar, Dining, Spa, Rooftop or PoolDeck.");
        }

        var enabled = request.Enabled ?? row?.Enabled ?? false;
        // Importing needs an account to read and a location to read it at: turned on without
        // them, the import would fail every few minutes and say nothing useful.
        if (enabled && row?.RefreshTokenProtected == null) return DomainErrors.Pos.SettingsInvalid("Connect Lightspeed before turning the import on.");
        if (enabled && locationId == null) return DomainErrors.Pos.SettingsInvalid("Choose the location whose tickets to import before turning the import on.");

        var now = DateTime.UtcNow;
        if (row == null)
        {
            row = new ClubPosSettings { Id = Guid.NewGuid(), OrganizationId = organizationId, Provider = PosProviders.LightspeedK, CreatedAtUtc = now };
            _db.ClubPosSettings.Add(row);
        }
        // Turned on for the first time with no start date: tickets from now on, so nothing closed
        // before the club connected is billed to anyone.
        if (enabled && !row.Enabled && importFrom == null) importFrom = now;
        row.Enabled = enabled;
        row.LocationId = locationId;
        row.LocationName = locationName;
        row.ImportFromUtc = importFrom;
        if (map != null) row.MethodMapJson = map.Count > 0 ? JsonSerializer.Serialize(map, Json) : null;
        row.DefaultVenue = venue;
        row.UpdatedAtUtc = now;
        row.UpdatedByUserId = userId;
        row.UpdatedByName = Who();
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A stale version, or two first saves at once: the other made the workspace's row.
            return DomainErrors.Pos.SettingsChanged;
        }
        return ToDto(row);
    }

    // ── connecting ───────────────────────────────────────────────────────────

    public Task<Result<string>> ConnectUrlAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        var provider = Provider(PosProviders.LightspeedK);
        if (!provider.IsConfigured) return Task.FromResult<Result<string>>(DomainErrors.Pos.NotConfigured);
        return Task.FromResult<Result<string>>(provider.AuthorizeUrl(_state.Create(organizationId, userId), RedirectUri));
    }

    public async Task<Result<PosSettingsDto>> CompleteConnectAsync(Guid organizationId, Guid userId, string? state, string? code, CancellationToken ct = default)
    {
        if (!_state.TryRead(state, out var startedIn, out var startedBy)) return DomainErrors.Pos.ConnectExpired;
        // A code that comes back with someone else's state, or to another workspace, is not this
        // person's connection to finish: it may be a link made to read one club's Lightspeed
        // account into another's workspace.
        if (startedIn != organizationId || startedBy != userId) return DomainErrors.Pos.ConnectNotYours;
        if (string.IsNullOrWhiteSpace(code)) return DomainErrors.Pos.ProviderRefused("Lightspeed did not give access to the account.");
        var provider = Provider(PosProviders.LightspeedK);
        if (!provider.IsConfigured) return DomainErrors.Pos.NotConfigured;
        var tokens = await provider.ExchangeCodeAsync(code.Trim(), RedirectUri, ct);
        if (tokens.Failure is { } failure)
            return failure.Unreachable ? DomainErrors.Pos.ProviderUnreachable(failure.Message) : DomainErrors.Pos.ProviderRefused(failure.Message);
        if (tokens.Value!.RefreshToken == null) return DomainErrors.Pos.ProviderRefused("Lightspeed gave no refresh token, so Cadence could not keep reading the account.");

        var now = DateTime.UtcNow;
        var row = await RowAsync(organizationId, ct);
        if (row == null)
        {
            row = new ClubPosSettings { Id = Guid.NewGuid(), OrganizationId = organizationId, Provider = provider.Name, CreatedAtUtc = now };
            _db.ClubPosSettings.Add(row);
        }
        row.Provider = provider.Name;
        row.RefreshTokenProtected = _secrets.Protect(tokens.Value.RefreshToken);
        row.AccessTokenProtected = _secrets.Protect(tokens.Value.AccessToken);
        row.AccessTokenExpiresAtUtc = tokens.Value.ExpiresAtUtc;
        row.ConnectedAtUtc = now;
        row.ConnectedByName = Who();
        row.LastImportError = null;
        row.UpdatedAtUtc = now;
        row.UpdatedByUserId = userId;
        row.UpdatedByName = Who();
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The settings saved by someone else meanwhile, or two first connections at once.
            return DomainErrors.Pos.SettingsChanged;
        }
        return ToDto(row);
    }

    public async Task<Result<PosSettingsDto>> DisconnectAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        var row = await RowAsync(organizationId, ct);
        if (row == null) return ToDto((ClubPosSettings?)null);
        row.RefreshTokenProtected = null;
        row.AccessTokenProtected = null;
        row.AccessTokenExpiresAtUtc = null;
        row.ConnectedAtUtc = null;
        row.ConnectedByName = null;
        row.Enabled = false;
        row.UpdatedAtUtc = DateTime.UtcNow;
        row.UpdatedByUserId = userId;
        row.UpdatedByName = Who();
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return DomainErrors.Pos.SettingsChanged;
        }
        return ToDto(row);
    }

    public async Task<Result<IReadOnlyList<PosLocationDto>>> LocationsAsync(Guid organizationId, CancellationToken ct = default)
    {
        var row = await RowAsync(organizationId, ct);
        if (row?.RefreshTokenProtected == null) return DomainErrors.Pos.NotConnected;
        var provider = Provider(row.Provider);
        var token = await AccessTokenAsync(row, provider, ct);
        if (!token.IsSuccess) return token.Error;
        var locations = await provider.LocationsAsync(token.Value, ct);
        if (locations.Failure is { } failure)
            return failure.Unreachable ? DomainErrors.Pos.ProviderUnreachable(failure.Message) : DomainErrors.Pos.ProviderRefused(failure.Message);
        return Result.Success(locations.Value!);
    }

    // ── tokens ───────────────────────────────────────────────────────────────

    /// <summary>
    /// An access token for the account: the one kept while it has a few minutes left, else a new
    /// one from the refresh token, kept with the new refresh token when Lightspeed gives one.
    /// </summary>
    /// <remarks>
    /// The new tokens are written by one UPDATE of their own columns, not a save of the row: an
    /// import refreshing them while someone saves the settings must not undo that save, or be
    /// refused for it.
    /// </remarks>
    private async Task<Result<string>> AccessTokenAsync(ClubPosSettings row, IPosProvider provider, CancellationToken ct)
    {
        if (row.AccessTokenExpiresAtUtc > DateTime.UtcNow.AddMinutes(2) && Reveal(row.AccessTokenProtected) is { } cached) return cached;
        var refresh = Reveal(row.RefreshTokenProtected);
        if (refresh == null) return DomainErrors.Pos.NotConnected;
        var tokens = await provider.RefreshAsync(refresh, ct);
        if (tokens.Failure is { } failure)
            return failure.Unreachable ? DomainErrors.Pos.ProviderUnreachable(failure.Message) : DomainErrors.Pos.ProviderRefused(failure.Message);
        var access = _secrets.Protect(tokens.Value!.AccessToken);
        var expires = tokens.Value.ExpiresAtUtc;
        var newRefresh = tokens.Value.RefreshToken is { } r ? _secrets.Protect(r) : row.RefreshTokenProtected;
        await UpdateRowAsync(row.Id,
            s => (s.AccessTokenProtected, s.AccessTokenExpiresAtUtc, s.RefreshTokenProtected) = (access, expires, newRefresh),
            q => q.ExecuteUpdateAsync(set => set
                .SetProperty(s => s.AccessTokenProtected, access)
                .SetProperty(s => s.AccessTokenExpiresAtUtc, expires)
                .SetProperty(s => s.RefreshTokenProtected, newRefresh), ct), ct);
        (row.AccessTokenProtected, row.AccessTokenExpiresAtUtc, row.RefreshTokenProtected) = (access, expires, newRefresh);
        return tokens.Value.AccessToken;
    }

    /// <summary>
    /// Changes the settings row by one UPDATE on SQL Server (no version check, no new version:
    /// bookkeeping such as the tokens or the import's progress must not refuse, or be refused
    /// by, a settings save); on the in-memory provider by a save of the row.
    /// </summary>
    private async Task UpdateRowAsync(Guid id, Action<ClubPosSettings> change, Func<IQueryable<ClubPosSettings>, Task<int>> update, CancellationToken ct)
    {
        var query = _db.ClubPosSettings.IgnoreQueryFilters().Where(s => s.Id == id);
        if (_db.Database.IsRelational())
        {
            await update(query);
            return;
        }
        var row = await query.FirstOrDefaultAsync(ct);
        if (row == null) return;
        change(row);
        await _db.SaveChangesAsync(ct);
    }

    private string? Reveal(string? protectedValue)
    {
        if (string.IsNullOrEmpty(protectedValue)) return null;
        try
        {
            return _secrets.Unprotect(protectedValue);
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(ex, "A saved POS token could not be decrypted; the Data Protection key ring may have changed");
            return null;
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private IPosProvider Provider(string? name) =>
        _providers.FirstOrDefault(p => p.Name == (name ?? PosProviders.LightspeedK)) ?? _providers.First();

    private async Task<ClubPosSettings?> RowAsync(Guid organizationId, CancellationToken ct) =>
        await _db.ClubPosSettings.FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct);

    private string? Who() => string.IsNullOrWhiteSpace(_user.UserName) ? null : Clip(_user.UserName.Trim(), 200);

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..max];

    private static Dictionary<string, string> MapOf(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var read = JsonSerializer.Deserialize<Dictionary<string, string>>(json, Json) ?? [];
            return new Dictionary<string, string>(read, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private PosSettingsDto ToDto(ClubPosSettings? row)
    {
        var connected = row?.RefreshTokenProtected != null;
        return new PosSettingsDto
        {
            Provider = row?.Provider ?? PosProviders.LightspeedK,
            ServerConfigured = Provider(row?.Provider).IsConfigured,
            Connected = connected,
            ConnectedAtUtc = connected ? row!.ConnectedAtUtc : null,
            ConnectedByName = connected ? row!.ConnectedByName : null,
            TokenUnreadable = connected && Reveal(row!.RefreshTokenProtected) == null,
            Enabled = row?.Enabled ?? false,
            LocationId = row?.LocationId,
            LocationName = row?.LocationName,
            ImportFromUtc = row?.ImportFromUtc,
            MethodMap = MapOf(row?.MethodMapJson),
            DefaultVenue = row?.DefaultVenue ?? "Dining",
            LastImportAtUtc = row?.LastImportAtUtc,
            LastImportError = row?.LastImportError,
            LastImportCount = row?.LastImportCount ?? 0,
            RedirectUri = RedirectUri,
            UpdatedAtUtc = row?.UpdatedAtUtc,
            UpdatedByName = row?.UpdatedByName,
            Version = row?.Version ?? Guid.Empty,
        };
    }
}
