using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

/// <inheritdoc />
/// <remarks>
/// Duplicate contacts used to be looked for in the browser, which compared contacts by
/// email and ruled out any two with different emails. A contact's email is unique in its
/// workspace, so on a real server it never found anything: the Duplicates page and the
/// Contacts banner always said "no duplicates". "Not the same" was kept in one browser and
/// forgotten in the next, so each colleague had to dismiss the same pair again.
/// </remarks>
public class DuplicateService : IDuplicateService
{
    /// <summary>The most records one decision names: plenty for a group, and a bound on the pairs it makes.</summary>
    public const int MaxRecordsPerDecision = 50;

    private readonly IDuplicateRepository _repository;
    private readonly ILogger<DuplicateService> _logger;

    public DuplicateService(IDuplicateRepository repository, ILogger<DuplicateService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DuplicateScanDto<ContactDto>> ScanContactsAsync(Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var candidates = await _repository.ContactCandidatesAsync(userId, organizationId, ct);
        var decisions = await _repository.DecisionsAsync(DuplicateKinds.Contacts, userId, organizationId, ct);
        var scan = DuplicateRules.Contacts(candidates.Select(Person), decisions.Select(d => (d.RecordIdA, d.RecordIdB)));
        var byId = candidates.ToDictionary(c => c.Id);
        return new DuplicateScanDto<ContactDto>(
            scan.Groups.Select(g => new DuplicateGroupDto<ContactDto>(g.Key, g.Reason, g.Ids.Select(id => ContactOf(byId[id])).ToList())).ToList(),
            scan.NotSame.Select(g => new NotSameGroupDto(g.Key, g.Ids)).ToList());
    }

    /// <inheritdoc />
    public async Task<DuplicateScanDto<CompanyDto>> ScanCompaniesAsync(Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var companies = await _repository.CompaniesAsync(userId, organizationId, ct);
        var decisions = await _repository.DecisionsAsync(DuplicateKinds.Companies, userId, organizationId, ct);
        var scan = DuplicateRules.Companies(
            companies.Select(c => new DuplicateRules.Firm(c.Id, MatchKeys.Domain(string.IsNullOrWhiteSpace(c.Domain) ? c.Website : c.Domain), MatchKeys.Name(c.Name))),
            decisions.Select(d => (d.RecordIdA, d.RecordIdB)));
        var byId = companies.ToDictionary(c => c.Id);
        return new DuplicateScanDto<CompanyDto>(
            scan.Groups.Select(g => new DuplicateGroupDto<CompanyDto>(g.Key, g.Reason, g.Ids.Select(id => CompanyOf(byId[id])).ToList())).ToList(),
            scan.NotSame.Select(g => new NotSameGroupDto(g.Key, g.Ids)).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<DuplicatePairDto>>> NotSameAsync(string kind, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        if (DuplicateKinds.Normalize(kind) is not { } known)
            return Result.Failure<IReadOnlyList<DuplicatePairDto>>(DomainErrors.Dedupe.InvalidKind);
        var decisions = await _repository.DecisionsAsync(known, userId, organizationId, ct);
        return Result.Success<IReadOnlyList<DuplicatePairDto>>(
            decisions.Select(d => new DuplicatePairDto(d.RecordIdA, d.RecordIdB, d.CreatedAtUtc)).ToList());
    }

    /// <inheritdoc />
    public async Task<Result> MarkNotSameAsync(string kind, IReadOnlyList<Guid> recordIds, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var checkedRequest = await CheckAsync(kind, recordIds, userId, organizationId, ct);
        if (checkedRequest.IsFailure) return checkedRequest.Error;
        var (known, pairs) = checkedRequest.Value;
        var added = await _repository.AddDecisionsAsync(known, pairs, userId, organizationId, ct);
        _logger.LogInformation("{Count} pairs of {Kind} marked not the same by user {UserId}", added, known, userId);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> ClearNotSameAsync(string kind, IReadOnlyList<Guid> recordIds, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        // Not checked against the workspace: a decision about a record since deleted can
        // be forgotten too, and only this workspace's decisions are touched.
        if (DuplicateKinds.Normalize(kind) is not { } known) return DomainErrors.Dedupe.InvalidKind;
        var ids = (recordIds ?? []).Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count < 2 || ids.Count > MaxRecordsPerDecision) return DomainErrors.Dedupe.RecordCount(MaxRecordsPerDecision);
        var removed = await _repository.RemoveDecisionsAsync(known, PairsOf(ids), userId, organizationId, ct);
        _logger.LogInformation("{Count} not-the-same decisions on {Kind} cleared by user {UserId}", removed, known, userId);
        return Result.Success();
    }

    private async Task<Result<(string Kind, IReadOnlyCollection<(Guid A, Guid B)> Pairs)>> CheckAsync(
        string kind, IReadOnlyList<Guid> recordIds, Guid userId, Guid? organizationId, CancellationToken ct)
    {
        if (DuplicateKinds.Normalize(kind) is not { } known)
            return Result.Failure<(string, IReadOnlyCollection<(Guid, Guid)>)>(DomainErrors.Dedupe.InvalidKind);
        var ids = (recordIds ?? []).Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count < 2 || ids.Count > MaxRecordsPerDecision)
            return Result.Failure<(string, IReadOnlyCollection<(Guid, Guid)>)>(DomainErrors.Dedupe.RecordCount(MaxRecordsPerDecision));
        // Records the server holds must be this workspace's: a decision cannot name
        // another workspace's people.
        var found = await _repository.CountInWorkspaceAsync(known, ids, userId, organizationId, ct);
        if (found is int count && count != ids.Count)
            return Result.Failure<(string, IReadOnlyCollection<(Guid, Guid)>)>(DomainErrors.Dedupe.RecordsNotInWorkspace);
        return Result.Success<(string, IReadOnlyCollection<(Guid, Guid)>)>((known, PairsOf(ids)));
    }

    private static IReadOnlyCollection<(Guid A, Guid B)> PairsOf(List<Guid> ids)
    {
        var pairs = new List<(Guid, Guid)>();
        for (var i = 0; i < ids.Count; i++)
            for (var j = i + 1; j < ids.Count; j++)
                pairs.Add(DuplicateRules.Pair(ids[i], ids[j]));
        return pairs;
    }

    /// <summary>A contact as the rules compare it, from what it holds now.</summary>
    private static DuplicateRules.Person Person(Contact c) => DuplicateRules.PersonOf(c.Id, c.Email, c.Phone, c.Name, c.CompanyId);

    private static ContactDto ContactOf(Contact e) =>
        new(e.Id, e.Name, e.Email, e.Phone, e.JobTitle, e.CompanyId, null, e.ConvertedFromLeadId, e.ConvertedAtUtc, e.IsArchived,
            e.DoNotContact, e.PreferredContactMethod, e.Company?.Name, e.CreatedAtUtc, e.UpdatedAtUtc, e.Description);

    private static CompanyDto CompanyOf(Company c) =>
        new(c.Id, c.Name, c.Domain, c.Industry, c.Size, c.Description, c.Website, c.Location, c.CreatedAtUtc, c.UpdatedAtUtc);
}
