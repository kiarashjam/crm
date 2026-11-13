using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class CompanyRepository : ICompanyRepository
{
    private readonly AppDbContext _db;

    public CompanyRepository(AppDbContext db) => _db = db;

    private static IQueryable<Company> FilterByUserAndOrg(IQueryable<Company> q, Guid userId, Guid? organizationId) =>
        organizationId == null
            ? q.Where(c => c.UserId == userId && c.OrganizationId == null)
            : q.Where(c => c.OrganizationId == organizationId);

    private IQueryable<Company> ApplySearch(IQueryable<Company> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var q = search.Trim().ToLowerInvariant();
        // Their custom fields' text and their tags' names too.
        var flexHits = FlexQueryable.SearchHits(_db, q);
        return query.Where(c => c.Name.ToLower().Contains(q)
            || (c.Domain != null && c.Domain.ToLower().Contains(q))
            || (c.Industry != null && c.Industry.ToLower().Contains(q))
            || flexHits.Contains(c.Id));
    }

    /// <summary>
    /// The companies list's filters and order, applied to the whole workspace in the database.
    /// The page filtered and sorted the twenty companies it had been sent, so "most contacts"
    /// ordered one page and an industry filter missed every other.
    /// </summary>
    private IQueryable<Company> Query(Guid userId, Guid? organizationId, CompanyQueryOptions options, FlexPlan plan)
    {
        var query = FlexQueryable.Filter(ApplySearch(FilterByUserAndOrg(_db.Companies.AsNoTracking(), userId, organizationId), options.Search), _db, plan);
        if (!string.IsNullOrWhiteSpace(options.Industry))
        {
            var industry = options.Industry.Trim().ToLower();
            query = query.Where(c => c.Industry != null && c.Industry.Trim().ToLower() == industry);
        }
        else if (options.HasIndustry == false)
            query = query.Where(c => c.Industry == null || c.Industry.Trim() == "");
        else if (options.HasIndustry == true)
            query = query.Where(c => c.Industry != null && c.Industry.Trim() != "");
        if (!string.IsNullOrWhiteSpace(options.Size))
        {
            var size = options.Size.Trim();
            query = query.Where(c => c.Size == size);
        }

        // Counted as the list shows them: active contacts, and deals the caller may see.
        var contacts = _db.Contacts.Where(x => !x.IsArchived);
        var deals = _db.Deals.VisibleTo(_db, userId, organizationId);
        if (FlexQueryable.Sort(query, _db, plan) is { } byField) return byField.ThenBy(c => c.Name).ThenBy(c => c.Id);
        var desc = string.Equals(options.SortDir?.Trim(), "desc", StringComparison.OrdinalIgnoreCase);
        return (options.SortBy?.Trim().ToLowerInvariant()) switch
        {
            "createdat" => desc
                ? query.OrderByDescending(c => c.CreatedAtUtc).ThenBy(c => c.Name).ThenBy(c => c.Id)
                : query.OrderBy(c => c.CreatedAtUtc).ThenBy(c => c.Name).ThenBy(c => c.Id),
            "contacts" => desc
                ? query.OrderByDescending(c => contacts.Count(x => x.CompanyId == c.Id)).ThenBy(c => c.Name).ThenBy(c => c.Id)
                : query.OrderBy(c => contacts.Count(x => x.CompanyId == c.Id)).ThenBy(c => c.Name).ThenBy(c => c.Id),
            "deals" => desc
                ? query.OrderByDescending(c => deals.Count(d => d.CompanyId == c.Id)).ThenBy(c => c.Name).ThenBy(c => c.Id)
                : query.OrderBy(c => deals.Count(d => d.CompanyId == c.Id)).ThenBy(c => c.Name).ThenBy(c => c.Id),
            _ => desc
                ? query.OrderByDescending(c => c.Name).ThenBy(c => c.Id)
                : query.OrderBy(c => c.Name).ThenBy(c => c.Id),
        };
    }

    public async Task<(IReadOnlyList<Company> Items, int TotalCount)> GetPagedAsync(
        Guid userId,
        Guid? organizationId,
        int skip,
        int take,
        CompanyQueryOptions options,
        CancellationToken ct = default)
    {
        var query = Query(userId, organizationId, options, await FlexQueryable.PlanAsync(_db, RecordTypes.Company, options.Flex, ct));
        var totalCount = await query.CountAsync(ct);
        var items = await query.Skip(skip).Take(take).ToListAsync(ct);
        return (items, totalCount);
    }

    public async Task<CompanySummaryDto> GetSummaryAsync(Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var weekAgo = DateTime.UtcNow.AddDays(-7);
        var companies = FilterByUserAndOrg(_db.Companies.AsNoTracking(), userId, organizationId);
        var deals = _db.Deals.VisibleTo(_db, userId, organizationId);

        var total = await companies.CountAsync(ct);
        var withContacts = await companies.CountAsync(c => _db.Contacts.Any(x => x.CompanyId == c.Id && !x.IsArchived), ct);
        var withDeals = await companies.CountAsync(c => deals.Any(d => d.CompanyId == c.Id), ct);
        var addedLast7Days = await companies.CountAsync(c => c.CreatedAtUtc >= weekAgo, ct);

        // Industries as typed, merged ignoring case and spaces: "Hospitality" and
        // "hospitality " are one industry, however the database compares text.
        var rows = await companies.GroupBy(c => c.Industry).Select(g => new { Industry = g.Key, Count = g.Count() }).ToListAsync(ct);
        var industries = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Industry))
            .GroupBy(r => r.Industry!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new IndustryCountDto(g.OrderByDescending(r => r.Count).First().Industry!.Trim(), g.Sum(r => r.Count)))
            .OrderByDescending(i => i.Count).ThenBy(i => i.Industry, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var withoutIndustry = rows.Where(r => string.IsNullOrWhiteSpace(r.Industry)).Sum(r => r.Count);

        var fallback = await FallbackCurrencyAsync(organizationId, ct);
        var companyIds = companies.Select(c => c.Id);
        var byCurrency = await deals
            .Where(d => d.CompanyId != null && companyIds.Contains(d.CompanyId.Value))
            .GroupBy(d => d.Currency == null || d.Currency.Trim() == "" ? fallback : d.Currency.Trim().ToUpper())
            .Select(g => new { Currency = g.Key, Priced = g.Count(d => d.Amount != null), Value = g.Sum(d => d.Amount ?? 0m) })
            .ToListAsync(ct);
        var dealTotals = byCurrency
            .Where(x => x.Priced > 0)
            .Select(x => new CurrencyTotalDto(x.Currency, x.Value, x.Priced))
            .OrderByDescending(t => t.Value).ThenBy(t => t.Currency, StringComparer.Ordinal)
            .ToList();

        return new CompanySummaryDto(total, withContacts, withDeals, addedLast7Days, dealTotals, industries, withoutIndustry);
    }

    public async Task<(IReadOnlyList<Company> Rows, int Total)> GetExportRowsAsync(
        Guid userId, Guid? organizationId, CompanyQueryOptions options, int max, CancellationToken ct = default)
    {
        var query = Query(userId, organizationId, options, await FlexQueryable.PlanAsync(_db, RecordTypes.Company, options.Flex, ct));
        var total = await query.CountAsync(ct);
        var rows = await query.Take(max).ToListAsync(ct);
        return (rows, total);
    }

    public async Task<int> CountAsync(Guid userId, Guid? organizationId, string? search = null, CancellationToken ct = default)
    {
        var query = FilterByUserAndOrg(_db.Companies, userId, organizationId);
        query = ApplySearch(query, search);
        return await query.CountAsync(ct);
    }

    public async Task<IReadOnlyList<Company>> GetByUserIdAsync(Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await FilterByUserAndOrg(_db.Companies, userId, organizationId)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

    // HP-13: Reuse ApplySearch to avoid duplicated filter logic
    public async Task<IReadOnlyList<Company>> SearchAsync(Guid userId, Guid? organizationId, string query, CancellationToken ct = default, int? take = null)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            if (take is not > 0)
                return await GetByUserIdAsync(userId, organizationId, ct);
            return await FilterByUserAndOrg(_db.Companies.AsNoTracking(), userId, organizationId)
                .OrderBy(c => c.Name)
                .Take(take.Value)
                .ToListAsync(ct);
        }
        var cap = take is > 0 ? take.Value : 20;
        var baseQuery = FilterByUserAndOrg(_db.Companies.AsNoTracking(), userId, organizationId);
        return await ApplySearch(baseQuery, query)
            .OrderBy(c => c.Name)
            .Take(cap)
            .ToListAsync(ct);
    }

    // HP-HIGH-2: Efficient duplicate name check — single SQL EXISTS query instead of loading all companies
    public async Task<bool> ExistsByNameAsync(string name, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var trimmedName = name.Trim().ToLowerInvariant();
        return await FilterByUserAndOrg(_db.Companies, userId, organizationId)
            .AnyAsync(c => c.Name.ToLower() == trimmedName, ct);
    }

    // Same scope as the list: in a workspace every member works the same companies.
    // Requiring the creator here meant a teammate saw a company in the list but got
    // "not found" opening or editing it.
    public async Task<Company?> GetByIdAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await FilterByUserAndOrg(_db.Companies, userId, organizationId).FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<Company> AddAsync(Company company, CancellationToken ct = default)
    {
        _db.Companies.Add(company);
        await _db.SaveChangesAsync(ct);
        return company;
    }

    public async Task<Company?> UpdateAsync(Company company, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var existing = await FilterByUserAndOrg(_db.Companies, userId, organizationId).FirstOrDefaultAsync(c => c.Id == company.Id, ct);
        if (existing == null) return null;
        _db.ExpectVersionOf(existing, company);
        existing.Name = company.Name;
        existing.Domain = company.Domain;
        existing.Industry = company.Industry;
        existing.Size = company.Size;
        existing.Description = company.Description;
        existing.Website = company.Website;
        existing.Location = company.Location;
        existing.UpdatedAtUtc = DateTime.UtcNow;
        existing.UpdatedByUserId = userId;
        await _db.SaveRefusingStaleAsync(ct);
        return existing;
    }

    // HP-7: Server-side stats per company using efficient SQL aggregations
    public async Task<IReadOnlyList<(Guid CompanyId, int ContactCount, int DealCount, IReadOnlyList<CurrencyTotalDto> DealTotals)>> GetStatsAsync(
        Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var companyIds = await FilterByUserAndOrg(_db.Companies, userId, organizationId)
            .Select(c => c.Id)
            .ToListAsync(ct);
        return await GetStatsAsync(companyIds, userId, organizationId, ct);
    }

    public async Task<IReadOnlyList<(Guid CompanyId, int ContactCount, int DealCount, IReadOnlyList<CurrencyTotalDto> DealTotals)>> GetStatsAsync(
        IReadOnlyCollection<Guid> companyIds, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        if (companyIds.Count == 0)
            return Array.Empty<(Guid, int, int, IReadOnlyList<CurrencyTotalDto>)>();

        // Contact counts via SQL GROUP BY — exclude archived contacts so dashboard counts match the visible list.
        var contactCounts = await _db.Contacts
            .Where(c => !c.IsArchived && c.CompanyId != null && companyIds.Contains(c.CompanyId.Value))
            .GroupBy(c => c.CompanyId!.Value)
            .Select(g => new { CompanyId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CompanyId, x => x.Count, ct);

        // Deal counts and values, added up by the database from Deal.Amount, per company
        // AND currency. Each value's text used to be stripped to digits and dots and summed
        // here, so francs and euros went into one figure and "1.234,56" counted as 1.23. A
        // deal with no readable amount is counted as a deal but adds nothing, and one with
        // no currency is in its workspace's, as new deals are.
        var fallback = await FallbackCurrencyAsync(organizationId, ct);
        var dealGroups = await _db.Deals.VisibleTo(_db, userId, organizationId)
            .Where(d => d.CompanyId != null && companyIds.Contains(d.CompanyId.Value))
            .GroupBy(d => new
            {
                CompanyId = d.CompanyId!.Value,
                Currency = d.Currency == null || d.Currency.Trim() == "" ? fallback : d.Currency.Trim().ToUpper(),
            })
            .Select(g => new
            {
                g.Key.CompanyId,
                g.Key.Currency,
                Count = g.Count(),
                Priced = g.Count(d => d.Amount != null),
                Value = g.Sum(d => d.Amount ?? 0m),
            })
            .ToListAsync(ct);

        var dealStats = dealGroups
            .GroupBy(g => g.CompanyId)
            .ToDictionary(
                g => g.Key,
                g => (Count: g.Sum(x => x.Count), Totals: (IReadOnlyList<CurrencyTotalDto>)g
                    .Where(x => x.Priced > 0)
                    .Select(x => new CurrencyTotalDto(x.Currency, x.Value, x.Priced))
                    .OrderByDescending(t => t.Value).ThenBy(t => t.Currency, StringComparer.Ordinal)
                    .ToList()));

        return companyIds.Select(id =>
        {
            var contacts = contactCounts.GetValueOrDefault(id, 0);
            var hasDealStats = dealStats.TryGetValue(id, out var ds);
            return (id, contacts, hasDealStats ? ds.Count : 0, hasDealStats ? ds.Totals : Array.Empty<CurrencyTotalDto>());
        }).ToList();
    }

    /// <summary>The currency a deal with none is in: its workspace's, as new deals are.</summary>
    private async Task<string> FallbackCurrencyAsync(Guid? organizationId, CancellationToken ct)
    {
        var workspaceCurrency = organizationId is Guid org
            ? await _db.Organizations.AsNoTracking().Where(o => o.Id == org).Select(o => o.Currency).FirstOrDefaultAsync(ct)
            : null;
        return RegionalSettings.TryCurrency(workspaceCurrency, out var code) ? code : RegionalSettings.DefaultCurrency;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var entity = await FilterByUserAndOrg(_db.Companies, userId, organizationId)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity == null) return false;
        // Null FKs so delete does not violate referential integrity (Contacts, Deals, Leads reference Company)
        await _db.Contacts.Where(c => c.CompanyId == id).ExecuteUpdateAsync(s => s.SetProperty(c => c.CompanyId, (Guid?)null), ct);
        await _db.Deals.Where(d => d.CompanyId == id).ExecuteUpdateAsync(s => s.SetProperty(d => d.CompanyId, (Guid?)null), ct);
        await _db.Leads.Where(l => l.CompanyId == id).ExecuteUpdateAsync(s => s.SetProperty(l => l.CompanyId, (Guid?)null), ct);
        // BUG #4 fix: Also nullify ConvertedToCompanyId on leads that were converted to this company
        await _db.Leads.Where(l => l.ConvertedToCompanyId == id).ExecuteUpdateAsync(s => s.SetProperty(l => l.ConvertedToCompanyId, (Guid?)null), ct);
        // Nothing of the club's keeps it (CompanyService asked): an empty account and past events let it go.
        await ClubClientRecords.ForgetDeletedCompanyAsync(_db, id, organizationId, ct);
        _db.Companies.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public Task<string?> KeptByClubAsync(Guid id, Guid? organizationId, CancellationToken ct = default) =>
        ClubClientRecords.WhyCompanyIsKeptAsync(_db, id, organizationId, ct);

    public Task<bool> MergeIntoAsync(Guid keepId, Guid dropId, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        RecordMerge.InTransactionAsync(_db, async () =>
        {
            if (keepId == dropId) return false;
            var keep = await FilterByUserAndOrg(_db.Companies, userId, organizationId).FirstOrDefaultAsync(c => c.Id == keepId, ct);
            var drop = await FilterByUserAndOrg(_db.Companies, userId, organizationId).FirstOrDefaultAsync(c => c.Id == dropId, ct);
            if (keep == null || drop == null) return false;

            foreach (var contact in await _db.Contacts.Where(c => c.CompanyId == dropId).ToListAsync(ct))
                contact.CompanyId = keepId;
            foreach (var deal in await _db.Deals.Where(d => d.CompanyId == dropId).ToListAsync(ct))
                deal.CompanyId = keepId;
            foreach (var lead in await _db.Leads.Where(l => l.CompanyId == dropId).ToListAsync(ct))
                lead.CompanyId = keepId;
            foreach (var lead in await _db.Leads.Where(l => l.ConvertedToCompanyId == dropId).ToListAsync(ct))
                lead.ConvertedToCompanyId = keepId;
            // Deleting the other company would delete its files and custom fields with it.
            await RecordMerge.MoveDependentsAsync(_db, RecordTypes.Company, keepId, dropId, ct);
            // Its corporate memberships, private events and house account (with every entry on it) are the kept company's.
            await ClubClientRecords.MoveCompanyAsync(_db, keepId, dropId, organizationId, keep.Name, ct);

            keep.Domain = RecordMerge.Fill(keep.Domain, drop.Domain);
            keep.Industry = RecordMerge.Fill(keep.Industry, drop.Industry);
            keep.Size = RecordMerge.Fill(keep.Size, drop.Size);
            keep.Description = RecordMerge.Fill(keep.Description, drop.Description);
            keep.Website = RecordMerge.Fill(keep.Website, drop.Website);
            keep.Location = RecordMerge.Fill(keep.Location, drop.Location);
            keep.UpdatedAtUtc = DateTime.UtcNow;
            keep.UpdatedByUserId = userId;
            await _db.SaveChangesAsync(ct);

            _db.Companies.Remove(drop);
            await _db.SaveChangesAsync(ct);
            return true;
        }, ct);
}
