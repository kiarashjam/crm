using ACI.Application.Common;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services;

/// <summary>
/// Tabs: items added while a tab is open, then one close that posts one charge to the
/// member's house account and the points it earns, together or not at all.
/// </summary>
/// <remarks>
/// An open tab is the one money record that changes as it goes, so it carries a Version:
/// two bartenders closing it from two devices both saw it open, and only the first save
/// lands. An item taken off stays on the record, marked removed, saying who took it off.
/// </remarks>
public sealed partial class ClubLedgerService
{
    public static readonly string[] TabVenues = ["Bar", "Dining", "Spa", "Rooftop", "PoolDeck"];

    public async Task<IReadOnlyList<TabDto>> TabsAsync(Guid? organizationId, CancellationToken ct = default)
    {
        var tabs = await _db.ClubTabs.AsNoTracking().Where(t => t.OrganizationId == organizationId).ToListAsync(ct);
        var items = await _db.ClubTabItems.AsNoTracking()
            .Where(i => i.OrganizationId == organizationId && i.RemovedAtUtc == null)
            .ToListAsync(ct);
        var byTab = items.GroupBy(i => i.TabId).ToDictionary(g => g.Key, g => g.ToList());
        return tabs.Select(t => ToDto(t, byTab.GetValueOrDefault(t.Id) ?? []))
            .OrderByDescending(t => t.OpenedAtUtc)
            .ToList();
    }

    public async Task<Result<TabDto>> OpenTabAsync(Guid? organizationId, OpenTabRequest request, CancellationToken ct = default)
    {
        // The tier sets the points the close earns, so it is the member's, not the request's.
        var member = await MemberAsync(organizationId, request.MemberId, ct);
        if (member == null) return DomainErrors.Club.MemberNotFound;
        var venue = (request.Venue ?? "").Trim();
        if (!TabVenues.Contains(venue)) return DomainErrors.Club.VenueInvalid;
        var server = Trimmed(request.ServerName);
        if (server?.Length > 200) return DomainErrors.Club.TooLong("A server's name", 200);
        var notes = Trimmed(request.Notes);
        if (notes?.Length > 1000) return DomainErrors.Club.TooLong("A note", 1000);

        var now = DateTime.UtcNow;
        var tab = new ClubTab
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            MemberId = member.Id,
            MemberName = NameOf(member),
            MemberTier = Truncate(member.Tier, 32),
            Venue = venue,
            ServerName = server,
            Notes = notes,
            Status = "Open",
            OpenedAtUtc = now,
            Total = 0,
            Currency = await AccountCurrencyAsync(organizationId, member.Id, ct),
            CreatedAtUtc = now,
            CreatedByUserId = _user.UserId,
            CreatedByName = Who(),
        };
        _db.ClubTabs.Add(tab);
        var saved = await SaveAsync(DomainErrors.Club.TabChanged, ct);
        if (!saved.IsSuccess) return saved.Error;
        return ToDto(tab, []);
    }

    /// <summary>Kinds of catalogue item a tab can ring up.</summary>
    private static readonly string[] TabKinds = [CatalogKinds.MenuItem, CatalogKinds.Product];

    public async Task<Result<TabDto>> AddTabItemAsync(Guid? organizationId, Guid tabId, AddTabItemRequest request, Guid? expectedVersion = null, CancellationToken ct = default)
    {
        var tab = await _db.ClubTabs.FirstOrDefaultAsync(t => t.Id == tabId && t.OrganizationId == organizationId, ct);
        if (tab == null) return DomainErrors.Club.TabNotFound;
        if ((expectedVersion ?? request.Version) is { } version && version != tab.Version) return DomainErrors.Club.TabChanged;
        if (tab.Status != "Open") return DomainErrors.Club.TabNotOpen(tab.Status);

        // From the menu, the name, the price and the VAT rate are the catalogue's; an item
        // typed in by hand (an open item) keeps the price it was given, as before.
        CatalogItemDto? menuItem = null;
        if (request.CatalogItemId is { } itemId)
        {
            var found = await _catalog.ChargeableAsync(organizationId, itemId, TabKinds, "a tab", ct);
            if (!found.IsSuccess) return found.Error;
            menuItem = found.Value;
            if (!string.Equals(menuItem.Currency, tab.Currency, StringComparison.OrdinalIgnoreCase))
                return DomainErrors.Club.CurrencyMismatch(menuItem.Currency, tab.Currency);
        }
        var name = menuItem?.Name ?? (request.Name ?? "").Trim();
        if (name.Length == 0) return DomainErrors.Club.TabItemNameRequired;
        if (name.Length > 200) return DomainErrors.Club.TooLong("An item name", 200);
        // A quantity of -3 took 45 off the tab with no reason recorded: a comp nobody
        // could trace. A half portion is fine; less than nothing, or a thousand, is not.
        if (request.Quantity is not { } quantity || quantity <= 0 || quantity > 1000 || quantity != Math.Round(quantity, 2))
            return DomainErrors.Club.TabQuantityInvalid;
        var unitPrice = menuItem?.Price ?? request.UnitPrice ?? -1;
        if (unitPrice < 0 || unitPrice > 100_000 || unitPrice != Math.Round(unitPrice, 2))
            return DomainErrors.Club.TabPriceInvalid;

        var items = await ActiveItemsAsync(tab.Id, ct);
        var item = new ClubTabItem
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            TabId = tab.Id,
            Name = name,
            Quantity = quantity,
            UnitPrice = unitPrice,
            CatalogItemId = menuItem?.Id,
            VatRate = menuItem?.VatRate,
            AddedAtUtc = DateTime.UtcNow,
            AddedByUserId = _user.UserId,
            AddedByName = Who(),
        };
        items.Add(item);
        tab.Total = TotalOf(items);
        Changed(tab);
        var saved = await SaveAsync(DomainErrors.Club.TabChanged, ct, () =>
        {
            _db.ClubTabItems.Add(item);
            return Task.CompletedTask;
        });
        if (!saved.IsSuccess) return saved.Error;
        return ToDto(tab, items);
    }

    public async Task<Result<TabDto>> RemoveTabItemAsync(Guid? organizationId, Guid tabId, Guid itemId, Guid? expectedVersion = null, CancellationToken ct = default)
    {
        var tab = await _db.ClubTabs.FirstOrDefaultAsync(t => t.Id == tabId && t.OrganizationId == organizationId, ct);
        if (tab == null) return DomainErrors.Club.TabNotFound;
        if (expectedVersion is { } version && version != tab.Version) return DomainErrors.Club.TabChanged;
        // Taking an item off a tab that has already posted left the posted charge as it
        // was, and the tab's history no longer matched the member's account.
        if (tab.Status != "Open") return DomainErrors.Club.TabNotOpen(tab.Status);
        var items = await ActiveItemsAsync(tab.Id, ct);
        var item = items.FirstOrDefault(i => i.Id == itemId);
        if (item == null) return DomainErrors.Club.TabItemNotFound;

        items.Remove(item);
        tab.Total = TotalOf(items);
        Changed(tab);
        var saved = await SaveAsync(DomainErrors.Club.TabChanged, ct, () =>
        {
            item.RemovedAtUtc = DateTime.UtcNow;
            item.RemovedByUserId = _user.UserId;
            item.RemovedByName = Who();
            return Task.CompletedTask;
        });
        if (!saved.IsSuccess) return saved.Error;
        return ToDto(tab, items);
    }

    public async Task<Result<TabDto>> CloseTabAsync(Guid? organizationId, Guid tabId, Guid? expectedVersion = null, CancellationToken ct = default)
    {
        // The club's points rules, read before anything here is changed (reading them may
        // save a new workspace's first tiers).
        var rules = await _rules.ReadAsync(organizationId, ct);
        var tab = await _db.ClubTabs.FirstOrDefaultAsync(t => t.Id == tabId && t.OrganizationId == organizationId, ct);
        if (tab == null) return DomainErrors.Club.TabNotFound;
        var items = await ActiveItemsAsync(tab.Id, ct);
        // Closing twice posts once: a double click, or a second device, gets the closed tab back.
        if (tab.Status == "Closed") return ToDto(tab, items);
        if (expectedVersion is { } version && version != tab.Version) return DomainErrors.Club.TabChanged;
        if (tab.Status != "Open") return DomainErrors.Club.TabNotOpen(tab.Status);
        var total = TotalOf(items);
        var label = VenueLabel(tab.Venue);

        // Everything the close writes is worked out first, so a refusal (a member who has
        // since left, say) comes before anything is saved.
        var member = await MemberAsync(organizationId, tab.MemberId, ct);
        if (total > 0 && member == null) return DomainErrors.Club.MemberNotFound;
        var account = member != null ? await AccountAsync(organizationId, member.Id, ct) : null;
        await _reads.ReadAsync("tabs", ct);

        ClubCharge? charge = null;
        if (total > 0)
        {
            charge = NewCharge(organizationId, member!, account!, ClubEntryTypes.Charge,
                tab.Venue switch { "Bar" => "Bar", "Spa" => "Spa", _ => "Dining" },
                $"{label} tab ({items.Count} items)", total);
            charge.TabId = tab.Id;
            // One VAT rate for the charge when every item on the tab came from the menu at
            // that rate; a mixed tab, or one with items typed in by hand, records none.
            var rates = items.Select(i => i.VatRate).Distinct().ToList();
            charge.VatRate = rates.Count == 1 ? rates[0] : null;
            Touch(account!);
            tab.ChargeId = charge.Id;
        }
        // The base points per unit spent and the tier's multiplier are the club's settings:
        // they used to be 10 and a table written into this method, whatever the club saved.
        // The member's tier as it is now: the tab keeps the name it was opened with, which a
        // tier renamed in the catalogue since (its members are renamed with it) no longer finds.
        var points = rules.PointsForSpend(total, member?.Tier ?? tab.MemberTier);
        LoyaltyLedgerEntry? earned = null;
        if (points > 0 && member != null)
        {
            earned = new LoyaltyLedgerEntry
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                MemberId = member.Id,
                MemberName = tab.MemberName,
                Kind = "Earned",
                Reason = tab.Venue == "Bar" ? "BarPurchase" : "DiningPurchase",
                Points = points,
                Note = $"{label} tab — {items.Count} items",
                CreatedAtUtc = DateTime.UtcNow,
            };
            tab.LoyaltyEntryId = earned.Id;
        }
        tab.Total = total;
        tab.PointsAwarded = points;
        tab.Status = "Closed";
        tab.ClosedAtUtc = DateTime.UtcNow;
        tab.ClosedByUserId = _user.UserId;
        tab.ClosedByName = Who();

        // Two bartenders closing the same tab both saw it open, and both posted a charge
        // and the points. The tab is saved first, checked against the open tab this request
        // read, then the charge, the balance and the points with it: the second close is
        // refused before it posts anything.
        var saved = await SaveAsync(DomainErrors.Club.TabChanged, ct, async () =>
        {
            if (charge != null)
            {
                _db.ClubCharges.Add(charge);
                await StageBalanceAsync(organizationId, member, -total, ct);
            }
            if (earned != null) _db.LoyaltyLedger.Add(earned);
        });
        if (!saved.IsSuccess) return saved.Error;
        return ToDto(tab, items);
    }

    public async Task<Result<TabDto>> VoidTabAsync(Guid? organizationId, Guid tabId, string? reason, Guid? expectedVersion = null, CancellationToken ct = default)
    {
        var tab = await _db.ClubTabs.FirstOrDefaultAsync(t => t.Id == tabId && t.OrganizationId == organizationId, ct);
        if (tab == null) return DomainErrors.Club.TabNotFound;
        var items = await ActiveItemsAsync(tab.Id, ct);
        if (tab.Status == "Voided") return ToDto(tab, items);
        // A stale screen used to void a tab someone else had closed: the tab said "no
        // charge", while the charge and the points it posted stayed on the account.
        if (tab.Status == "Closed") return DomainErrors.Club.TabClosedCannotVoid;
        if (tab.Status != "Open") return DomainErrors.Club.TabNotOpen(tab.Status);
        if (expectedVersion is { } version && version != tab.Version) return DomainErrors.Club.TabChanged;
        var why = Trimmed(reason);
        if (why?.Length > 500) return DomainErrors.Club.TooLong("A reason", 500);

        tab.Status = "Voided";
        tab.VoidedAtUtc = DateTime.UtcNow;
        tab.VoidedByUserId = _user.UserId;
        tab.VoidedByName = Who();
        tab.VoidReason = why;
        var saved = await SaveAsync(DomainErrors.Club.TabChanged, ct);
        if (!saved.IsSuccess) return saved.Error;
        return ToDto(tab, items);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<List<ClubTabItem>> ActiveItemsAsync(Guid tabId, CancellationToken ct) =>
        await _db.ClubTabItems.Where(i => i.TabId == tabId && i.RemovedAtUtc == null).OrderBy(i => i.AddedAtUtc).ToListAsync(ct);

    /// <summary>Saves the tab as changed even when no column of its own moved (an item at no charge), so its version moves.</summary>
    private void Changed(ClubTab tab)
    {
        var entry = _db.Entry(tab);
        if (entry.State == EntityState.Unchanged) entry.State = EntityState.Modified;
    }

    private static decimal TotalOf(IEnumerable<ClubTabItem> items) =>
        Math.Round(items.Sum(i => i.Quantity * i.UnitPrice), 2, MidpointRounding.AwayFromZero);

    private static string VenueLabel(string venue) => venue switch
    {
        "Bar" => "Library Bar",
        "Dining" => "Main Dining",
        "PoolDeck" => "Pool Deck",
        _ => venue,
    };

    private static TabDto ToDto(ClubTab tab, IEnumerable<ClubTabItem> items) => new()
    {
        Id = tab.Id,
        MemberId = tab.MemberId,
        MemberName = tab.MemberName,
        MemberTier = tab.MemberTier,
        Venue = tab.Venue,
        ServerName = tab.ServerName,
        Notes = tab.Notes,
        Items = items.OrderBy(i => i.AddedAtUtc).Select(i => new TabItemDto
        {
            Id = i.Id, Name = i.Name, Quantity = i.Quantity, UnitPrice = i.UnitPrice, CatalogItemId = i.CatalogItemId, VatRate = i.VatRate,
            AddedAtUtc = i.AddedAtUtc, AddedByUserId = i.AddedByUserId, AddedByName = i.AddedByName,
        }).ToList(),
        Status = tab.Status,
        OpenedAtUtc = tab.OpenedAtUtc,
        Total = tab.Total,
        Currency = tab.Currency,
        ClosedAtUtc = tab.ClosedAtUtc,
        ClosedByUserId = tab.ClosedByUserId,
        ClosedByName = tab.ClosedByName,
        ChargeId = tab.ChargeId,
        LoyaltyEntryId = tab.LoyaltyEntryId,
        PointsAwarded = tab.Status == "Closed" ? tab.PointsAwarded : null,
        VoidedAtUtc = tab.VoidedAtUtc,
        VoidedByUserId = tab.VoidedByUserId,
        VoidedByName = tab.VoidedByName,
        VoidReason = tab.VoidReason,
        Version = tab.Version,
    };
}
