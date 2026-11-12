using System.Text.Json;
using System.Text.Json.Nodes;
using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services;

/// <summary>The club's price catalogue (see <see cref="IClubCatalog"/>).</summary>
public sealed class ClubCatalogService : IClubCatalog
{
    private const decimal MaxMoney = 1_000_000_000m;

    /// <summary>
    /// The four tiers the settings page has always shown, which a workspace with no tiers of
    /// its own is given. The migration that made the catalogue wrote the same ones out for
    /// the workspaces it carried over (AddClubCatalogAndBilling.Tiers).
    /// </summary>
    public static readonly IReadOnlyList<(string Name, decimal Dues, decimal Fee, decimal Multiplier, string[] Benefits)> DefaultTiers =
    [
        ("Bronze", 49m, 0m, 1.00m, ["Coworking access", "Member rate on events", "Member directory"]),
        ("Silver", 129m, 250m, 1.25m, ["All Bronze benefits", "Spa access", "Fitness studio", "Rooftop access"]),
        ("Gold", 299m, 1000m, 1.50m, ["All Silver benefits", "Private dining priority", "Guest passes (4/mo)", "Tennis court"]),
        ("Platinum", 599m, 2500m, 2.00m, ["All Gold benefits", "Unlimited guest passes", "Comp spa treatment monthly", "Founders events"]),
    ];

    private readonly AppDbContext _db;
    private readonly ICurrentUserService _user;

    public ClubCatalogService(AppDbContext db, ICurrentUserService user)
    {
        _db = db;
        _user = user;
    }

    // ── reads ────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<CatalogItemDto>> ListAsync(Guid? organizationId, string? kind = null, bool includeInactive = true, CancellationToken ct = default)
    {
        await EnsureTiersAsync(organizationId, ct);
        var rows = await _db.ClubCatalogItems.AsNoTracking()
            .Where(i => i.OrganizationId == organizationId && (kind == null || i.Kind == kind) && (includeInactive || i.IsActive))
            .ToListAsync(ct);
        return Ordered(rows).Select(ToDto).ToList();
    }

    public async Task<Result<CatalogItemDto>> GetAsync(Guid? organizationId, Guid id, CancellationToken ct = default)
    {
        var row = await _db.ClubCatalogItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id && i.OrganizationId == organizationId, ct);
        return row == null ? DomainErrors.Catalog.NotFound : ToDto(row);
    }

    public async Task<IReadOnlyList<CatalogItemDto>> TiersAsync(Guid? organizationId, CancellationToken ct = default)
    {
        var rows = await _db.ClubCatalogItems.AsNoTracking()
            .Where(i => i.OrganizationId == organizationId && i.Kind == CatalogKinds.Membership)
            .ToListAsync(ct);
        if (rows.Count > 0 || organizationId == null) return Ordered(rows).Select(ToDto).ToList();
        return Ordered(await EnsureTiersAsync(organizationId, ct)).Select(ToDto).ToList();
    }

    public async Task<CatalogItemDto?> TierAsync(Guid? organizationId, string? name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var wanted = name.Trim();
        return (await TiersAsync(organizationId, ct)).FirstOrDefault(t => string.Equals(t.Name, wanted, StringComparison.OrdinalIgnoreCase));
    }

    public Task<string> CurrencyAsync(Guid? organizationId, CancellationToken ct = default) =>
        WorkspaceCurrencyAsync(organizationId, ct);

    public async Task<Result<CatalogItemDto>> ChargeableAsync(Guid? organizationId, Guid id, IReadOnlyCollection<string> kinds, string use, CancellationToken ct = default)
    {
        var found = await GetAsync(organizationId, id, ct);
        if (!found.IsSuccess) return found.Error;
        var item = found.Value;
        if (!kinds.Contains(item.Kind)) return DomainErrors.Catalog.NotForThis(item.Kind, use);
        if (!item.IsActive) return DomainErrors.Catalog.NotOnSale(item.Name);
        return item;
    }

    // ── writes ───────────────────────────────────────────────────────────────

    public async Task<Result<CatalogItemDto>> CreateAsync(Guid? organizationId, CatalogItemWriteRequest request, CancellationToken ct = default)
    {
        var kind = CatalogKinds.All.FirstOrDefault(k => string.Equals(k, request.Kind?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (kind == null) return DomainErrors.Catalog.KindInvalid;
        await EnsureTiersAsync(organizationId, ct);
        var now = DateTime.UtcNow;
        var item = new ClubCatalogItem
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Kind = kind,
            Currency = await WorkspaceCurrencyAsync(organizationId, ct),
            Cadence = CatalogKinds.IsRecurring(kind) ? "Monthly" : null,
            LoyaltyMultiplier = kind == CatalogKinds.Membership ? 1m : null,
            InitiationFee = kind == CatalogKinds.Membership ? 0m : null,
            IsActive = true,
            CreatedAtUtc = now,
            CreatedByUserId = _user.UserId,
            CreatedByName = Who(),
        };
        if (request.Price == null) return DomainErrors.Catalog.PriceInvalid;
        if (await ApplyAsync(organizationId, item, request, isNew: true, ct) is { } invalid) return invalid;
        if (request.SortOrder == null)
        {
            // At the end of its kind, as a new tier would be added below the others.
            var last = await _db.ClubCatalogItems.Where(i => i.OrganizationId == organizationId && i.Kind == kind)
                .Select(i => (int?)i.SortOrder).MaxAsync(ct);
            item.SortOrder = (last ?? -1) + 1;
        }
        item.UpdatedAtUtc = now;
        item.UpdatedByUserId = _user.UserId;
        item.UpdatedByName = Who();
        _db.ClubCatalogItems.Add(item);
        var saved = await SaveAsync(ct, kind == CatalogKinds.Membership ? DomainErrors.Catalog.TierNameTaken(item.Name) : DomainErrors.Catalog.Changed);
        return saved.IsSuccess ? ToDto(item) : saved.Error;
    }

    public async Task<Result<CatalogItemDto>> UpdateAsync(Guid? organizationId, Guid id, CatalogItemWriteRequest request, Guid? expectedVersion = null, CancellationToken ct = default)
    {
        var item = await _db.ClubCatalogItems.FirstOrDefaultAsync(i => i.Id == id && i.OrganizationId == organizationId, ct);
        if (item == null) return DomainErrors.Catalog.NotFound;
        if ((expectedVersion ?? request.Version) is { } version && version != item.Version) return DomainErrors.Catalog.Changed;
        if (request.Kind != null && !string.Equals(request.Kind.Trim(), item.Kind, StringComparison.OrdinalIgnoreCase))
            return DomainErrors.Catalog.KindFixed;
        var oldName = item.Name;
        if (await ApplyAsync(organizationId, item, request, isNew: false, ct) is { } invalid) return invalid;

        // Members carry their tier by name, so a renamed tier is renamed on them in the same
        // save: they keep the tier, and nobody is left on a name the catalogue no longer has.
        if (item.Kind == CatalogKinds.Membership && !string.Equals(oldName, item.Name, StringComparison.Ordinal))
        {
            var members = await _db.ClubMembers.Where(m => m.OrganizationId == organizationId && m.Tier == oldName).ToListAsync(ct);
            foreach (var member in members.Where(m => string.Equals(m.Tier, oldName, StringComparison.OrdinalIgnoreCase)))
                member.Tier = item.Name;
        }
        item.UpdatedAtUtc = DateTime.UtcNow;
        item.UpdatedByUserId = _user.UserId;
        item.UpdatedByName = Who();
        var saved = await SaveAsync(ct, DomainErrors.Catalog.Changed);
        return saved.IsSuccess ? ToDto(item) : saved.Error;
    }

    /// <summary>Checks the request and copies what it sends onto <paramref name="item"/>. Null when all is well.</summary>
    private async Task<Error?> ApplyAsync(Guid? organizationId, ClubCatalogItem item, CatalogItemWriteRequest r, bool isNew, CancellationToken ct)
    {
        var isTier = item.Kind == CatalogKinds.Membership;
        if (r.Name != null || isNew)
        {
            var name = (r.Name ?? "").Trim();
            if (name.Length == 0) return DomainErrors.Catalog.NameRequired;
            // A tier's name is what members carry, in a column of 32.
            if (isTier && name.Length > 32) return DomainErrors.Club.TooLong("A tier's name", 32);
            if (name.Length > 200) return DomainErrors.Club.TooLong("A name", 200);
            if (isTier)
            {
                var taken = await _db.ClubCatalogItems.AsNoTracking()
                    .Where(i => i.OrganizationId == organizationId && i.Kind == CatalogKinds.Membership && i.Id != item.Id)
                    .Select(i => i.Name).ToListAsync(ct);
                if (taken.Any(t => string.Equals(t, name, StringComparison.OrdinalIgnoreCase))) return DomainErrors.Catalog.TierNameTaken(name);
            }
            item.Name = name;
        }
        if (r.Description != null)
        {
            if (r.Description.Trim().Length > 1000) return DomainErrors.Club.TooLong("A description", 1000);
            item.Description = Trimmed(r.Description);
        }
        if (r.Category != null)
        {
            if (r.Category.Trim().Length > 60) return DomainErrors.Club.TooLong("A category", 60);
            item.Category = Trimmed(r.Category);
        }
        if (r.Code != null)
        {
            if (r.Code.Trim().Length > 64) return DomainErrors.Club.TooLong("A code", 64);
            item.Code = Trimmed(r.Code);
        }
        if (r.Price != null)
        {
            if (Money(r.Price.Value) is not { } price) return DomainErrors.Catalog.PriceInvalid;
            item.Price = price;
        }
        if (r.Currency != null)
        {
            if (!RegionalSettings.TryCurrency(r.Currency, out var currency)) return DomainErrors.Catalog.CurrencyInvalid;
            item.Currency = currency;
        }
        if (r.VatRate != null)
        {
            var vat = r.VatRate.Value;
            if (vat is < 0 or > 100 || vat != Math.Round(vat, 2)) return DomainErrors.Catalog.VatInvalid;
            item.VatRate = vat;
        }
        if (r.Cadence != null && CatalogKinds.IsRecurring(item.Kind))
        {
            var cadence = ClubLedgerService.Cadences.FirstOrDefault(c => string.Equals(c, r.Cadence.Trim(), StringComparison.OrdinalIgnoreCase));
            if (cadence == null) return DomainErrors.Catalog.CadenceInvalid;
            item.Cadence = cadence;
        }
        if (isTier)
        {
            if (r.InitiationFee != null)
            {
                if (Money(r.InitiationFee.Value) is not { } fee) return DomainErrors.Catalog.PriceInvalid;
                item.InitiationFee = fee;
            }
            if (r.LoyaltyMultiplier != null)
            {
                var multiplier = r.LoyaltyMultiplier.Value;
                // A multiplier of 0 used to be impossible to save (the page turned it into 1);
                // here it is refused in words instead.
                if (multiplier is <= 0 or > 100 || multiplier != Math.Round(multiplier, 2)) return DomainErrors.Catalog.MultiplierInvalid;
                item.LoyaltyMultiplier = multiplier;
            }
            if (r.Benefits != null)
            {
                var benefits = r.Benefits.Select(b => (b ?? "").Trim()).Where(b => b.Length > 0).ToList();
                if (benefits.Count > 20 || benefits.Any(b => b.Length > 200)) return DomainErrors.Catalog.BenefitsInvalid;
                item.Benefits = benefits.Count == 0 ? null : JsonSerializer.Serialize(benefits);
            }
        }
        if (isTier || item.Kind == CatalogKinds.Dues)
        {
            // Dues are charged as dues, whatever the request says.
            item.ChargeKind = "Dues";
        }
        else if (r.ChargeKind != null)
        {
            var chargeKind = ClubLedgerService.ChargeKinds.FirstOrDefault(k => string.Equals(k, r.ChargeKind.Trim(), StringComparison.OrdinalIgnoreCase));
            if (chargeKind == null) return DomainErrors.Catalog.ChargeKindInvalid;
            item.ChargeKind = chargeKind;
        }
        else if (isNew)
        {
            item.ChargeKind = DefaultChargeKind(item.Kind, item.Category);
        }
        if (r.IsActive != null) item.IsActive = r.IsActive.Value;
        if (r.SortOrder != null)
        {
            if (r.SortOrder is < 0 or > 10_000) return DomainErrors.Catalog.SortOrderInvalid;
            item.SortOrder = r.SortOrder.Value;
        }
        return null;
    }

    /// <summary>What a charge from an item of this kind is posted as, unless the item says otherwise.</summary>
    public static string DefaultChargeKind(string kind, string? category) => kind switch
    {
        CatalogKinds.Membership or CatalogKinds.Dues => "Dues",
        CatalogKinds.Product => "Retail",
        CatalogKinds.Ticket => "Event",
        CatalogKinds.MenuItem => category?.Trim() switch
        {
            { } c when c.Equals("Bar", StringComparison.OrdinalIgnoreCase) => "Bar",
            { } c when c.Equals("Spa", StringComparison.OrdinalIgnoreCase) => "Spa",
            _ => "Dining",
        },
        _ => "Other",
    };

    // ── a workspace's first tiers ────────────────────────────────────────────

    /// <summary>
    /// Gives a workspace with no tiers in the catalogue the ones its saved settings list, or
    /// the four defaults: the same rule the migration carried existing workspaces over by.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A tier is never deleted, only taken off sale, so a workspace with none has never had
    /// any: this runs once for each. Two first reads at the same moment both try; the unique
    /// tier name lets one save, and the other finds its tiers there.
    /// </para>
    /// <para>
    /// It saves only when nothing else is waiting to be saved in this request: a save here
    /// would carry the caller's half-made changes with it. Then the tiers it would give are
    /// answered without being kept, and the next read keeps them. Every caller that needs a
    /// tier's id (a subscription started from it) reads the tiers before changing anything.
    /// </para>
    /// </remarks>
    /// <returns>The workspace's tiers: the ones it had, or the ones it was just given.</returns>
    private async Task<IReadOnlyList<ClubCatalogItem>> EnsureTiersAsync(Guid? organizationId, CancellationToken ct)
    {
        // The club is an organization's: a personal workspace has no tiers to give.
        if (organizationId == null) return [];
        var existing = await _db.ClubCatalogItems.AsNoTracking()
            .Where(i => i.OrganizationId == organizationId && i.Kind == CatalogKinds.Membership).ToListAsync(ct);
        if (existing.Count > 0) return existing;

        var settings = await _db.ClubDocuments.AsNoTracking()
            .Where(d => d.OrganizationId == organizationId && d.Kind == ClubSettings.Kind)
            .OrderByDescending(d => d.UpdatedAtUtc)
            .Select(d => d.Payload)
            .FirstOrDefaultAsync(ct);
        var tiers = TiersFrom(settings);
        var currency = await WorkspaceCurrencyAsync(organizationId, ct);
        var now = DateTime.UtcNow;
        var rows = tiers.Select((t, i) => new ClubCatalogItem
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Kind = CatalogKinds.Membership,
            Name = t.Name,
            Price = t.Dues,
            Currency = currency,
            VatRate = 0,
            Cadence = "Monthly",
            InitiationFee = t.Fee,
            LoyaltyMultiplier = t.Multiplier,
            Benefits = t.Benefits.Length == 0 ? null : JsonSerializer.Serialize(t.Benefits),
            ChargeKind = "Dues",
            IsActive = true,
            SortOrder = t.Position,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        }).ToList();
        if (_db.ChangeTracker.HasChanges()) return rows;
        _db.ClubCatalogItems.AddRange(rows);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Another request gave the workspace its tiers first.
            foreach (var row in rows) _db.Entry(row).State = EntityState.Detached;
            return await _db.ClubCatalogItems.AsNoTracking()
                .Where(i => i.OrganizationId == organizationId && i.Kind == CatalogKinds.Membership).ToListAsync(ct);
        }
        foreach (var row in rows) _db.Entry(row).State = EntityState.Detached;
        return rows;
    }

    /// <summary>
    /// The tiers a saved settings document lists, read as the migration reads them: a name
    /// cut to 32, dues and fee from 0 to under a billion (else 0), a multiplier above 0 and
    /// under 1,000 (else 1), each name once; or the defaults when it lists none.
    /// </summary>
    public static IReadOnlyList<(string Name, decimal Dues, decimal Fee, decimal Multiplier, string[] Benefits, int Position)> TiersFrom(string? payload)
    {
        var listed = new List<(string Name, decimal Dues, decimal Fee, decimal Multiplier, string[] Benefits, int Position)>();
        JsonArray? array = null;
        try
        {
            array = (payload == null ? null : JsonNode.Parse(payload) as JsonObject)?["tiers"] as JsonArray;
        }
        catch (JsonException)
        {
        }
        if (array != null)
        {
            for (var i = 0; i < array.Count; i++)
            {
                if (array[i] is not JsonObject t) continue;
                var name = (t["tier"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null)?.Trim() ?? "";
                if (name.Length == 0) continue;
                if (name.Length > 32) name = name[..32];
                if (listed.Any(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase))) continue;
                var dues = Decimal(t["monthlyDues"]) is { } d && d >= 0 && d < MaxMoney ? Math.Round(d, 2) : 0m;
                var fee = Decimal(t["initiationFee"]) is { } f && f >= 0 && f < MaxMoney ? Math.Round(f, 2) : 0m;
                var multiplier = Decimal(t["loyaltyMultiplier"]) is { } m && m > 0 && m < 1000 ? Math.Round(m, 2) : 1m;
                var benefits = t["benefits"] is JsonArray b
                    ? b.Select(x => x is JsonValue xv && xv.TryGetValue<string>(out var text) ? text : null).Where(x => x != null).Select(x => x!).ToArray()
                    : [];
                listed.Add((name, dues, fee, multiplier, benefits, i));
            }
        }
        return listed.Count > 0
            ? listed
            : DefaultTiers.Select((t, i) => (t.Name, t.Dues, t.Fee, t.Multiplier, t.Benefits, i)).ToList();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<Result> SaveAsync(CancellationToken ct, Error conflict)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            Forget();
            return conflict;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            Forget();
            return conflict;
        }
    }

    private void Forget()
    {
        foreach (var entry in _db.ChangeTracker.Entries()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList())
            entry.State = EntityState.Detached;
    }

    private static bool IsUniqueViolation(DbUpdateException ex) => ex.InnerException is SqlException { Number: 2601 or 2627 };

    private async Task<string> WorkspaceCurrencyAsync(Guid? organizationId, CancellationToken ct)
    {
        if (organizationId == null) return RegionalSettings.DefaultCurrency;
        var currency = await _db.Organizations.AsNoTracking().Where(o => o.Id == organizationId).Select(o => o.Currency).FirstOrDefaultAsync(ct);
        return RegionalSettings.TryCurrency(currency, out var code) ? code : RegionalSettings.DefaultCurrency;
    }

    private static IEnumerable<ClubCatalogItem> Ordered(IEnumerable<ClubCatalogItem> rows) =>
        rows.OrderBy(i => Array.IndexOf(CatalogKinds.All, i.Kind))
            .ThenBy(i => i.SortOrder)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase);

    private static decimal? Money(decimal value)
    {
        if (value < 0 || value >= MaxMoney) return null;
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal? Decimal(JsonNode? node)
    {
        if (node is not JsonValue value) return null;
        if (value.TryGetValue<decimal>(out var d)) return d;
        if (value.TryGetValue<double>(out var x) && !double.IsNaN(x) && !double.IsInfinity(x) && Math.Abs(x) < 1e15) return (decimal)x;
        return value.TryGetValue<string>(out var s) && decimal.TryParse(s, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var p) ? p : null;
    }

    private string? Who()
    {
        var name = _user.UserName?.Trim();
        if (string.IsNullOrEmpty(name)) return null;
        return name.Length > 200 ? name[..200] : name;
    }

    private static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static CatalogItemDto ToDto(ClubCatalogItem i) => new()
    {
        Id = i.Id,
        Kind = i.Kind,
        Name = i.Name,
        Description = i.Description,
        Category = i.Category,
        Code = i.Code,
        Price = i.Price,
        Currency = i.Currency,
        VatRate = i.VatRate,
        Cadence = i.Cadence,
        InitiationFee = i.Kind == CatalogKinds.Membership ? i.InitiationFee ?? 0 : null,
        LoyaltyMultiplier = i.Kind == CatalogKinds.Membership ? i.LoyaltyMultiplier ?? 1 : null,
        Benefits = ReadBenefits(i.Benefits),
        ChargeKind = i.ChargeKind,
        IsActive = i.IsActive,
        SortOrder = i.SortOrder,
        CreatedAtUtc = i.CreatedAtUtc,
        UpdatedAtUtc = i.UpdatedAtUtc,
        UpdatedByName = i.UpdatedByName,
        Version = i.Version,
    };

    private static IReadOnlyList<string> ReadBenefits(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json)?.Where(b => !string.IsNullOrWhiteSpace(b)).ToList() ?? (IReadOnlyList<string>)Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
