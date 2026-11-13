using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ACI.Application.Common;
using ACI.Application.Common.I18n;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ACI.Infrastructure.Services;

/// <summary>Membership applications, approval and the public form (see <see cref="IClubApplications"/>).</summary>
/// <remarks>
/// <para>
/// Approving used to be three requests from the browser: create a member, then mark the
/// application approved. A retry after the second failed, or two reviewers pressing Approve at
/// once, made two members; and nothing started the dues or charged the initiation fee, so the
/// new member was never billed and stayed Pending for good.
/// </para>
/// <para>
/// Here it is one transaction. It starts by claiming the application: the status, the member it
/// will make (an id chosen now) and a short lease are saved against the version this request
/// read. On SQL Server a second approval waits on that row until the first commits, then finds
/// the application approved and answers with its member; where there are no transactions (the
/// in-memory test database) the lease tells the second to wait for the first. Everything after
/// the claim (the member, the links, the household, the consent, the dues, the fee, the welcome)
/// commits with it or not at all.
/// </para>
/// </remarks>
public sealed class ClubApplicationService : IClubApplications
{
    /// <summary>How long a claim holds an application for the request that made it.</summary>
    private static readonly TimeSpan ClaimFor = TimeSpan.FromMinutes(2);

    /// <summary>The checkbox's words when the club wrote none, in English (see <see cref="MemberTexts"/> for German and French).</summary>
    internal static readonly string DefaultConsentText = MemberTexts.T(AppLanguages.English, "form.applicationConsent");
    private const string LeadSource = "Membership application";

    private readonly AppDbContext _db;
    private readonly IClubLedger _ledger;
    private readonly IClubCatalog _catalog;
    private readonly IClubRules _rules;
    private readonly IOutbox _outbox;
    private readonly IConsentRepository _consent;
    private readonly IPermissionService _permissions;
    private readonly IWorkspaceRecords _records;
    private readonly ILeadService _leads;
    private readonly ILeadOpsService _leadOps;
    private readonly INotificationService _notifications;
    private readonly ITenantContext _tenant;
    private readonly ILogger<ClubApplicationService> _logger;
    private readonly ILedgerReadObserver _reads;
    private readonly IWorkspaceModules _modules;
    private readonly IPrivacySettings? _privacy;
    private readonly IAutomationEngine? _automations;

    public ClubApplicationService(AppDbContext db, IClubLedger ledger, IClubCatalog catalog, IClubRules rules, IOutbox outbox,
        IConsentRepository consent, IPermissionService permissions, IWorkspaceRecords records, ILeadService leads,
        ILeadOpsService leadOps, INotificationService notifications, ITenantContext tenant, ILogger<ClubApplicationService> logger,
        ILedgerReadObserver reads, IWorkspaceModules modules, IPrivacySettings? privacy = null,
        IAutomationEngine? automations = null)
    {
        _automations = automations;
        _db = db;
        _ledger = ledger;
        _catalog = catalog;
        _rules = rules;
        _outbox = outbox;
        _consent = consent;
        _permissions = permissions;
        _records = records;
        _leads = leads;
        _leadOps = leadOps;
        _notifications = notifications;
        _tenant = tenant;
        _logger = logger;
        _reads = reads;
        _modules = modules;
        _privacy = privacy;
    }

    /// <summary>Whether the workspace's public forms ask for marketing consent (its privacy settings); optional when it set nothing.</summary>
    private async Task<string> ConsentModeAsync(Guid organizationId, CancellationToken ct) =>
        _privacy == null ? MarketingConsentModes.Optional : (await _privacy.FormConsentAsync(organizationId, ct)).Mode;

    // ── reads ────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ApplicationDto>> ListAsync(Guid organizationId, CancellationToken ct = default)
    {
        var rows = await _db.ClubApplications.AsNoTracking()
            .Where(a => a.OrganizationId == organizationId)
            .OrderByDescending(a => a.SubmittedAtUtc).ThenBy(a => a.Id)
            .ToListAsync(ct);
        return await MapAsync(organizationId, rows, ct);
    }

    public async Task<Result<ApplicationDto>> GetAsync(Guid organizationId, Guid applicationId, CancellationToken ct = default)
    {
        var row = await _db.ClubApplications.AsNoTracking().FirstOrDefaultAsync(a => a.Id == applicationId && a.OrganizationId == organizationId, ct);
        return row == null ? DomainErrors.Application.NotFound : (await MapAsync(organizationId, [row], ct))[0];
    }

    public async Task<IReadOnlyList<ApplicationDto>> ForPersonAsync(Guid organizationId, Guid? leadId, Guid? contactId, Guid? memberId, CancellationToken ct = default)
    {
        if (leadId == null && contactId == null && memberId == null) return [];
        var rows = await _db.ClubApplications.AsNoTracking()
            .Where(a => a.OrganizationId == organizationId
                && ((leadId != null && a.LeadId == leadId) || (contactId != null && a.ContactId == contactId) || (memberId != null && a.MemberId == memberId)))
            .OrderByDescending(a => a.SubmittedAtUtc)
            .ToListAsync(ct);
        return await MapAsync(organizationId, rows, ct);
    }

    public async Task<PersonMembershipDto> MembershipAsync(Guid organizationId, Guid? leadId, Guid? contactId, CancellationToken ct = default)
    {
        // The lead's contact, or the contact's lead: a member may be linked by either.
        if (leadId is { } lid)
            contactId ??= await _db.Leads.AsNoTracking().Where(l => l.Id == lid && l.OrganizationId == organizationId)
                .Select(l => l.ConvertedToContactId).FirstOrDefaultAsync(ct);
        else if (contactId is { } cid)
            leadId = await _db.Contacts.AsNoTracking().Where(c => c.Id == cid && c.OrganizationId == organizationId)
                .Select(c => c.ConvertedFromLeadId).FirstOrDefaultAsync(ct);
        var members = leadId == null && contactId == null ? [] : await _db.ClubMembers.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId
                && ((leadId != null && m.LeadId == leadId) || (contactId != null && m.ContactId == contactId)))
            .OrderBy(m => m.JoinedAtUtc)
            .Select(m => new MemberSummaryDto(m.Id, (m.FirstName + " " + m.LastName).Trim(), m.Email, m.Tier, m.Status, m.JoinedAtUtc))
            .ToListAsync(ct);
        return new PersonMembershipDto
        {
            ContactId = contactId,
            Members = members,
            Applications = await ForPersonAsync(organizationId, leadId, contactId, null, ct),
        };
    }

    public async Task<Result<MemberLinksDto>> MemberLinksAsync(Guid organizationId, Guid memberId, Guid? viewerId = null, CancellationToken ct = default)
    {
        var member = await _db.ClubMembers.AsNoTracking()
            .Where(m => m.Id == memberId && m.OrganizationId == organizationId)
            .Select(m => new { m.Id, m.LeadId, m.ContactId, m.DealId }).FirstOrDefaultAsync(ct);
        if (member == null) return DomainErrors.Club.MemberNotFound;
        var lead = member.LeadId is { } lid
            ? await _db.Leads.AsNoTracking().Where(l => l.Id == lid && l.OrganizationId == organizationId).Select(l => new { l.Id, l.Name, l.Status }).FirstOrDefaultAsync(ct)
            : null;
        var contact = member.ContactId is { } cid
            ? await _db.Contacts.AsNoTracking().Where(c => c.Id == cid && c.OrganizationId == organizationId).Select(c => new { c.Id, c.Name, c.Email }).FirstOrDefaultAsync(ct)
            : null;

        // Their contracts once sent: the ones that admitted them, the ones whose deposit went on
        // their account, and those of the lead and the deal they came from.
        var admittedBy = await _db.ClubApplications.AsNoTracking()
            .Where(a => a.OrganizationId == organizationId && a.MemberId == member.Id && a.ContractId != null)
            .Select(a => a.ContractId!.Value).ToListAsync(ct);
        var paidBy = await _db.ContractDeposits.AsNoTracking()
            .Where(d => d.OrganizationId == organizationId && d.MemberId == member.Id)
            .Select(d => d.ContractId).ToListAsync(ct);
        var named = admittedBy.Concat(paidBy).Distinct().ToList();
        var contracts = await _db.Contracts.AsNoTracking()
            .Where(c => c.OrganizationId == organizationId && c.Status != ContractStatuses.Draft
                && (named.Contains(c.Id) || (member.LeadId != null && c.LeadId == member.LeadId) || (member.DealId != null && c.DealId == member.DealId)))
            .OrderByDescending(c => c.SentAtUtc ?? c.CreatedAtUtc)
            .Select(c => new { c.Id, c.Title, c.Status, c.MembershipTier, c.SentAtUtc, c.CounterSignedAtUtc, c.LeadId, c.DealId })
            .ToListAsync(ct);

        // A deal the viewer may not open is not named here, nor are the contracts drafted on it,
        // as the deal's own contract list does not show them.
        var dealIds = contracts.Where(c => c.DealId != null).Select(c => c.DealId!.Value)
            .Concat(member.DealId is { } own ? [own] : Array.Empty<Guid>()).Distinct().ToList();
        var deals = dealIds.Count == 0 ? new Dictionary<Guid, string>() : await (viewerId is { } viewer
                ? _db.Deals.AsNoTracking().VisibleTo(_db, viewer, organizationId)
                : _db.Deals.AsNoTracking().Where(d => d.OrganizationId == organizationId))
            .Where(d => dealIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Name, ct);
        var shown = contracts.Where(c => c.DealId is not { } d || deals.ContainsKey(d)).ToList();
        var shownIds = shown.Select(c => c.Id).ToList();
        var deposits = shownIds.Count == 0 ? new Dictionary<Guid, ContractDeposit>() : await _db.ContractDeposits.AsNoTracking()
            .Where(d => d.OrganizationId == organizationId && shownIds.Contains(d.ContractId))
            .ToDictionaryAsync(d => d.ContractId, ct);

        return new MemberLinksDto
        {
            MemberId = member.Id,
            LeadId = lead?.Id,
            LeadName = lead?.Name,
            LeadStatus = lead?.Status,
            ContactId = contact?.Id,
            ContactName = contact?.Name,
            ContactEmail = string.IsNullOrWhiteSpace(contact?.Email) ? null : contact.Email,
            DealId = member.DealId is { } dealId && deals.ContainsKey(dealId) ? dealId : null,
            DealName = member.DealId is { } shownDeal ? deals.GetValueOrDefault(shownDeal) : null,
            Applications = await ForPersonAsync(organizationId, null, null, member.Id, ct),
            Contracts = shown.Select(c => new MemberContractDto
            {
                Id = c.Id,
                Title = c.Title,
                Status = c.Status,
                MembershipTier = c.MembershipTier,
                SentAtUtc = Utc(c.SentAtUtc),
                CounterSignedAtUtc = Utc(c.CounterSignedAtUtc),
                LeadId = c.LeadId,
                DealId = c.DealId,
                Deposit = deposits.GetValueOrDefault(c.Id) is { } d ? DepositOf(d) : null,
            }).ToList(),
        };
    }

    public async Task<PersonMembershipDto> DealMembershipAsync(Guid organizationId, Guid dealId, CancellationToken ct = default)
    {
        // The deal's contact, and the lead it came from: the person the deal is with.
        var contactId = await _db.Deals.AsNoTracking().Where(d => d.Id == dealId && d.OrganizationId == organizationId)
            .Select(d => d.ContactId).FirstOrDefaultAsync(ct);
        var leadId = await _db.Leads.AsNoTracking().Where(l => l.OrganizationId == organizationId && l.ConvertedToDealId == dealId)
            .OrderByDescending(l => l.UpdatedAtUtc ?? l.CreatedAtUtc).Select(l => (Guid?)l.Id).FirstOrDefaultAsync(ct);
        var person = await MembershipAsync(organizationId, leadId, contactId, ct);

        // And whoever the deal brought in: by its handoff, or admitted by one of its contracts.
        var contractIds = await _db.Contracts.AsNoTracking().Where(c => c.OrganizationId == organizationId && c.DealId == dealId)
            .Select(c => c.Id).ToListAsync(ct);
        var byContract = contractIds.Count == 0 ? [] : await _db.ClubApplications.AsNoTracking()
            .Where(a => a.OrganizationId == organizationId && a.ContractId != null && contractIds.Contains(a.ContractId.Value))
            .ToListAsync(ct);
        var admitted = byContract.Where(a => a.MemberId != null).Select(a => a.MemberId!.Value).ToList();
        var known = person.Members.Select(m => m.Id).ToHashSet();
        var more = await _db.ClubMembers.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && (m.DealId == dealId || admitted.Contains(m.Id)))
            .OrderBy(m => m.JoinedAtUtc)
            .Select(m => new MemberSummaryDto(m.Id, (m.FirstName + " " + m.LastName).Trim(), m.Email, m.Tier, m.Status, m.JoinedAtUtc))
            .ToListAsync(ct);
        var applications = person.Applications.ToList();
        var unlisted = byContract.Where(a => applications.All(x => x.Id != a.Id)).ToList();
        if (unlisted.Count > 0) applications.AddRange(await MapAsync(organizationId, unlisted, ct));
        return person with
        {
            Members = person.Members.Concat(more.Where(m => !known.Contains(m.Id))).ToList(),
            Applications = applications.OrderByDescending(a => a.SubmittedAtUtc).ToList(),
        };
    }

    /// <summary>A contract's deposit as the member page shows it.</summary>
    private static ContractDepositDto DepositOf(ContractDeposit d) => new()
    {
        Amount = d.Amount,
        Currency = d.Currency,
        Status = d.Status,
        AmountPaid = d.AmountPaid,
        AmountRefunded = d.AmountRefunded,
        PaidAtUtc = Utc(d.PaidAtUtc),
        Provider = d.Provider,
        MethodType = d.MethodType,
        MemberId = d.MemberId,
        LedgerPaymentId = d.LedgerPaymentId,
        RecordedAtUtc = Utc(d.RecordedAtUtc),
    };

    private static DateTime? Utc(DateTime? at) => at is null ? null : DateTime.SpecifyKind(at.Value, DateTimeKind.Utc);

    // ── staff changes ────────────────────────────────────────────────────────

    public async Task<Result<ApplicationDto>> CreateAsync(Guid organizationId, Guid userId, ApplicationWriteRequest request, CancellationToken ct = default)
    {
        var row = new ClubApplication
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Status = ApplicationStatuses.Submitted,
            Source = ApplicationSources.Staff,
            SubmittedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId,
        };
        var applied = await ApplyAsync(organizationId, userId, row, request, creating: true, ct);
        if (!applied.IsSuccess) return applied.Error;
        // Typed in without a lead or contact: the person the sales side already has, when there is one.
        if (row.LeadId == null && row.ContactId == null)
        {
            var person = await PersonByEmailAsync(organizationId, row.Email, $"{row.FirstName} {row.LastName}", requireName: true, ct);
            row.ContactId = person.ContactId;
            row.LeadId = person.LeadId;
        }
        _db.ClubApplications.Add(row);
        await _db.SaveChangesAsync(ct);
        return (await MapAsync(organizationId, [row], ct))[0];
    }

    public async Task<Result<ApplicationDto>> UpdateAsync(Guid organizationId, Guid applicationId, Guid userId, ApplicationWriteRequest request, CancellationToken ct = default)
    {
        var row = await TrackedAsync(organizationId, applicationId, ct);
        if (row == null) return DomainErrors.Application.NotFound;
        if (request.Version is { } version && version != Guid.Empty && version != row.Version) return DomainErrors.Application.Changed;
        if (row.Status == ApplicationStatuses.Approved) return DomainErrors.Application.ApprovedLocked;
        var applied = await ApplyAsync(organizationId, userId, row, request, creating: false, ct);
        if (!applied.IsSuccess) return applied.Error;
        if (!string.IsNullOrWhiteSpace(request.Status) && !string.Equals(request.Status.Trim(), row.Status, StringComparison.OrdinalIgnoreCase))
        {
            var moved = Move(row, request.Status.Trim(), userId, null);
            if (moved != null) return moved;
        }
        return await SaveAndMapAsync(organizationId, row, ct);
    }

    public async Task<Result<ApplicationDto>> SetStatusAsync(Guid organizationId, Guid applicationId, Guid userId, ApplicationStatusRequest request, CancellationToken ct = default)
    {
        var row = await TrackedAsync(organizationId, applicationId, ct);
        if (row == null) return DomainErrors.Application.NotFound;
        if (request.Version is { } version && version != Guid.Empty && version != row.Version) return DomainErrors.Application.Changed;
        var notes = Trimmed(request.ReviewerNotes);
        if (notes?.Length > 2000) return DomainErrors.Application.TooLong("The committee notes", 2000);
        var moved = Move(row, (request.Status ?? "").Trim(), userId, notes);
        if (moved != null) return moved;
        return await SaveAndMapAsync(organizationId, row, ct);
    }

    public async Task<Result> DeleteAsync(Guid organizationId, Guid applicationId, CancellationToken ct = default)
    {
        var row = await TrackedAsync(organizationId, applicationId, ct);
        if (row == null) return DomainErrors.Application.NotFound;
        // The member an approved application made stays; only the record of the application goes.
        _db.ClubApplications.Remove(row);
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<ApplicationDto>> StartForPersonAsync(Guid organizationId, Guid userId, Guid? leadId, Guid? contactId, CancellationToken ct = default)
    {
        string name;
        string email;
        string? phone;
        if (leadId is { } lid)
        {
            if (!await _records.ExistsAsync(RecordTypes.Lead, lid, userId, organizationId, ct)) return DomainErrors.Link.LeadNotFound;
            var lead = await _db.Leads.AsNoTracking().Where(l => l.Id == lid && l.OrganizationId == organizationId)
                .Select(l => new { l.Name, l.Email, l.Phone, l.ConvertedToContactId }).FirstAsync(ct);
            (name, email, phone) = (lead.Name, lead.Email, lead.Phone);
            contactId ??= lead.ConvertedToContactId;
        }
        else if (contactId is { } cid)
        {
            if (!await _records.ExistsAsync(RecordTypes.Contact, cid, userId, organizationId, ct)) return DomainErrors.Link.ContactNotFound;
            var contact = await _db.Contacts.AsNoTracking().Where(c => c.Id == cid && c.OrganizationId == organizationId)
                .Select(c => new { c.Name, c.Email, c.Phone, c.ConvertedFromLeadId }).FirstAsync(ct);
            (name, email, phone) = (contact.Name, contact.Email, contact.Phone);
            leadId = contact.ConvertedFromLeadId;
        }
        else return DomainErrors.Link.LeadNotFound;

        // The open application they already have, rather than a second one.
        var open = await _db.ClubApplications.AsNoTracking()
            .Where(a => a.OrganizationId == organizationId
                && (a.Status == ApplicationStatuses.Submitted || a.Status == ApplicationStatuses.UnderReview || a.Status == ApplicationStatuses.Waitlisted)
                && ((leadId != null && a.LeadId == leadId) || (contactId != null && a.ContactId == contactId)))
            .OrderByDescending(a => a.SubmittedAtUtc).FirstOrDefaultAsync(ct);
        if (open != null) return (await MapAsync(organizationId, [open], ct))[0];

        var (first, last) = SplitName(name);
        var tier = (await _catalog.TiersAsync(organizationId, ct)).FirstOrDefault(t => t.IsActive);
        var row = new ClubApplication
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            FirstName = Clip(first, 100),
            LastName = Clip(last, 100),
            Email = Clip((email ?? "").Trim(), 256),
            Phone = Trimmed(phone) is { } p ? Clip(p, 64) : null,
            RequestedTier = tier?.Name ?? "",
            Status = ApplicationStatuses.Submitted,
            Source = ApplicationSources.Lead,
            SubmittedAtUtc = DateTime.UtcNow,
            LeadId = leadId,
            ContactId = contactId,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId,
        };
        _db.ClubApplications.Add(row);
        await _db.SaveChangesAsync(ct);
        return (await MapAsync(organizationId, [row], ct))[0];
    }

    // ── approval ─────────────────────────────────────────────────────────────

    /// <summary>Who is approving and what they may do.</summary>
    private sealed record Approval(Guid UserId, string Trigger, bool MayHandleMoney, ApproveApplicationRequest Request, Guid? ContractId = null, Guid? DepositPaymentId = null);

    public async Task<Result<ApprovalResultDto>> ApproveAsync(Guid organizationId, Guid applicationId, Guid userId, ApproveApplicationRequest request, CancellationToken ct = default)
    {
        var held = await _permissions.PermissionsAsync(userId, organizationId, ct);
        return await ApproveCoreAsync(organizationId, applicationId,
            new Approval(userId, ApprovalTriggers.Staff, held.Contains(MemberPermissions.ManageMoney), request ?? new ApproveApplicationRequest()), ct);
    }

    public async Task<IReadOnlyList<ApprovalResultDto>> AdmitForContractAsync(Guid organizationId, Guid contractId, Guid? leadId, Guid? dealId, Guid userId, CancellationToken ct = default)
    {
        // A workspace that has turned its club off admits nobody from the Sales side either, as
        // a deal's handoff adds nobody to its members (OnboardingService): the member, their
        // initiation fee and their dues would be posted to a club whose pages and endpoints are
        // gone, where nobody could see or bill them. The contract is still executed; the
        // application stays open for a reviewer once the club is on again.
        if (!await _modules.IsOnAsync(organizationId, WorkspaceModules.Club, ct))
        {
            _logger.LogInformation("Contract {ContractId} was countersigned while the club is turned off; any application behind it stays open for a reviewer", contractId);
            return [];
        }

        // The tier the contract admits its counterparty at makes it a membership contract: its
        // countersignature asks the club for that member at that tier, with or without an
        // application behind it. A contract with no tier and no application (private dining,
        // say) admits nobody.
        var contract = await _db.Contracts.AsNoTracking()
            .Where(c => c.Id == contractId && c.OrganizationId == organizationId)
            .Select(c => new { c.MembershipTier, c.CounterpartyName, c.CounterpartyEmail })
            .FirstOrDefaultAsync(ct);
        var tier = Trimmed(contract?.MembershipTier);

        // The application behind the contract: one it names, or the open one of its lead, of the
        // lead that became its deal, or of their contact or the deal's.
        var (leadIds, contactIds) = await PeopleOfAsync(organizationId, leadId, dealId, ct);
        var candidates = await _db.ClubApplications
            .Where(a => a.OrganizationId == organizationId
                && (a.ContractId == contractId || (a.LeadId != null && leadIds.Contains(a.LeadId.Value))
                    || (a.ContactId != null && contactIds.Contains(a.ContactId.Value)))
                && (a.Status == ApplicationStatuses.Submitted || a.Status == ApplicationStatuses.UnderReview
                    || a.Status == ApplicationStatuses.Waitlisted || (a.Status == ApplicationStatuses.Approved && a.MoneyPending)))
            .OrderByDescending(a => a.SubmittedAtUtc)
            .ToListAsync(ct);

        // Linked before anyone is admitted: the contract each open application is for, and the
        // tier it was signed at. A reviewer who approves it later (after a sales rep's
        // countersignature) admits at the tier signed, and the deposit finds it by its contract.
        foreach (var open in candidates.Where(a => a.Status != ApplicationStatuses.Approved))
        {
            open.ContractId = contractId;
            if (tier != null && !string.Equals(open.RequestedTier, tier, StringComparison.OrdinalIgnoreCase)) open.RequestedTier = Clip(tier, 32);
            open.UpdatedAtUtc = DateTime.UtcNow;
        }
        if (candidates.Count > 0)
        {
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Changed by a reviewer at this moment: the approval below reads it again.
                foreach (var row in candidates) _db.Entry(row).State = EntityState.Detached;
            }
        }
        var ids = candidates.Select(a => a.Id).ToList();

        // A membership contract with no application behind it starts one, once: the club's way
        // to a member, its rules and its reviewers' list. The contract's own (one approved
        // already) is not started again.
        if (ids.Count == 0 && tier != null && contract != null
            && !await _db.ClubApplications.AnyAsync(a => a.OrganizationId == organizationId && a.ContractId == contractId, ct))
        {
            Guid? signerLead = leadId ?? (leadIds.Count > 0 ? leadIds[0] : null);
            Guid? signerContact = contactIds.Count > 0 ? contactIds[0] : null;
            var started = await StartForContractAsync(organizationId, contractId, contract.CounterpartyName, contract.CounterpartyEmail, tier,
                signerLead, signerContact, userId, ct);
            if (started != null) ids.Add(started.Value);
        }

        // Countersigning needs only records.edit, which a sales rep has. The countersignature is
        // an approval by that person, so it is held to the approve button's rules: admitting a
        // member needs club.members, and starting the dues and charging the fee needs
        // money.manage. Without club.members the contract is still executed (ContractService
        // does that whatever happens here), but the application stays open for someone who may
        // admit members; without money.manage the member is admitted with the money left
        // pending, as a reviewer's approval would leave it.
        var held = await _permissions.PermissionsAsync(userId, organizationId, ct);
        if (!held.Contains(MemberPermissions.ClubMembers))
        {
            _logger.LogInformation("Contract {ContractId} was countersigned by someone who may not admit members; any application behind it stays open for a reviewer", contractId);
            return [];
        }
        var mayHandleMoney = held.Contains(MemberPermissions.ManageMoney);

        var results = new List<ApprovalResultDto>();
        foreach (var id in ids)
        {
            // At the tier signed, whatever the application asked for: the contract is what both sides agreed.
            var approved = await ApproveCoreAsync(organizationId, id,
                new Approval(userId, ApprovalTriggers.Contract, mayHandleMoney, new ApproveApplicationRequest { Tier = tier }, ContractId: contractId), ct);
            if (approved.IsSuccess) results.Add(approved.Value);
            else _logger.LogWarning("Contract {ContractId} was countersigned but application {ApplicationId} could not be approved: {Error}",
                contractId, id, approved.Error.Description);
        }
        return results;
    }

    /// <summary>
    /// Whom a contract is with, as the sales side knows them: its lead, the leads that became its
    /// deal, and their contacts and the deal's own.
    /// </summary>
    private async Task<(List<Guid> LeadIds, List<Guid> ContactIds)> PeopleOfAsync(Guid organizationId, Guid? leadId, Guid? dealId, CancellationToken ct)
    {
        var leadIds = new List<Guid>();
        var contactIds = new List<Guid>();
        if (leadId is { } l) leadIds.Add(l);
        if (dealId is { } d)
        {
            leadIds.AddRange(await _db.Leads.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.ConvertedToDealId == d).Select(x => x.Id).ToListAsync(ct));
            if (await _db.Deals.AsNoTracking().Where(x => x.Id == d && x.OrganizationId == organizationId).Select(x => x.ContactId).FirstOrDefaultAsync(ct) is { } dealContact)
                contactIds.Add(dealContact);
        }
        if (leadIds.Count > 0)
            contactIds.AddRange(await _db.Leads.AsNoTracking()
                .Where(x => x.OrganizationId == organizationId && leadIds.Contains(x.Id) && x.ConvertedToContactId != null)
                .Select(x => x.ConvertedToContactId!.Value).ToListAsync(ct));
        return (leadIds.Distinct().ToList(), contactIds.Distinct().ToList());
    }

    /// <summary>
    /// The application a countersigned membership contract with none behind it starts: for the
    /// person who signed it, at the tier they signed for, linked to the contract and to the
    /// contract's lead and contact.
    /// </summary>
    private async Task<Guid?> StartForContractAsync(Guid organizationId, Guid contractId, string signedBy, string email, string tier,
        Guid? leadId, Guid? contactId, Guid userId, CancellationToken ct)
    {
        string? phone = null;
        if (leadId is { } lid)
            phone = await _db.Leads.AsNoTracking().Where(l => l.Id == lid && l.OrganizationId == organizationId).Select(l => l.Phone).FirstOrDefaultAsync(ct);
        if (phone == null && contactId is { } cid)
            phone = await _db.Contacts.AsNoTracking().Where(c => c.Id == cid && c.OrganizationId == organizationId).Select(c => c.Phone).FirstOrDefaultAsync(ct);
        var (first, last) = SplitName(signedBy);
        if (first.Length == 0)
        {
            _logger.LogWarning("Contract {ContractId} names a tier but nobody who signed it; no application was started", contractId);
            return null;
        }
        var now = DateTime.UtcNow;
        var row = new ClubApplication
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            FirstName = Clip(first, 100),
            LastName = Clip(last, 100),
            Email = Clip((email ?? "").Trim(), 256),
            Phone = Trimmed(phone) is { } p ? Clip(p, 64) : null,
            RequestedTier = Clip(tier, 32),
            Status = ApplicationStatuses.Submitted,
            Source = ApplicationSources.Contract,
            SubmittedAtUtc = now,
            LeadId = leadId,
            ContactId = contactId,
            ContractId = contractId,
            CreatedAtUtc = now,
            CreatedByUserId = userId,
        };
        _db.ClubApplications.Add(row);
        await _db.SaveChangesAsync(ct);
        return row.Id;
    }

    public Task<Result<ApprovalResultDto>> AdmitForDepositAsync(Guid organizationId, Guid applicationId, Guid userId, Guid? depositPaymentId, CancellationToken ct = default) =>
        ApproveCoreAsync(organizationId, applicationId,
            new Approval(userId, ApprovalTriggers.Deposit, true, new ApproveApplicationRequest(), DepositPaymentId: depositPaymentId), ct);

    private async Task<Result<ApprovalResultDto>> ApproveCoreAsync(Guid organizationId, Guid applicationId, Approval approval, CancellationToken ct)
    {
        var request = approval.Request;
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var app = await TrackedAsync(organizationId, applicationId, ct);
            if (app == null) return DomainErrors.Application.NotFound;
            var now = DateTime.UtcNow;
            var claimed = app.ApprovalClaimedUntilUtc > now;

            if (app.Status == ApplicationStatuses.Approved)
            {
                // Approved already: a retry, a double click, a second reviewer. Answer with the
                // member; finish the dues and fee when they were left for someone who may.
                if (claimed)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100 * (attempt + 1)), ct);
                    continue;
                }
                if (!(app.MoneyPending && approval.MayHandleMoney))
                    return await AnswerAsync(organizationId, app, new Outcome(), ct);
                // Null when someone else finished it at the same moment: read it again.
                if (await FinishMoneyAsync(organizationId, app, approval, ct) is { } finished) return finished;
                await Task.Delay(TimeSpan.FromMilliseconds(100 * (attempt + 1)), ct);
                continue;
            }
            if (!ApplicationStatuses.IsOpen(app.Status)) return DomainErrors.Application.NotOpen(app.Status);
            if (request.Version is { } version && version != Guid.Empty && version != app.Version) return DomainErrors.Application.Changed;
            if (claimed)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100 * (attempt + 1)), ct);
                continue;
            }

            // What can refuse is decided before anything is written: the tier, its currency, the household.
            var plan = await PlanAsync(organizationId, app, approval, ct);
            if (!plan.IsSuccess) return plan.Error;
            // Read and decided, not yet claimed: where a test holds several approvals at once.
            await _reads.ReadAsync("applications", ct);

            var transaction = _db.Database.IsRelational() && _db.Database.CurrentTransaction == null
                ? await _db.Database.BeginTransactionAsync(ct)
                : null;
            try
            {
                // The claim. Saved against the version read above: of two approvals at once, the
                // second waits for the first (SQL Server) or finds its version gone, and starts over.
                var previousStatus = app.Status;
                app.Status = ApplicationStatuses.Approved;
                app.MemberId = plan.Value.Existing?.Id ?? app.MemberId ?? Guid.NewGuid();
                app.ApprovedAtUtc = now;
                app.ApprovedByUserId = approval.UserId;
                app.ApprovalTrigger = approval.Trigger;
                app.ContractId = approval.ContractId ?? app.ContractId;
                app.DepositPaymentId = approval.DepositPaymentId ?? app.DepositPaymentId;
                app.ReviewedAtUtc = now;
                app.ReviewedByUserId = approval.UserId;
                if (Trimmed(request.ReviewerNotes) is { } notes) app.ReviewerNotes = Clip(notes, 2000);
                app.ApprovalClaimedUntilUtc = now + ClaimFor;
                app.UpdatedAtUtc = now;
                try
                {
                    await _db.SaveChangesAsync(ct);
                }
                catch (DbUpdateConcurrencyException)
                {
                    _db.Entry(app).State = EntityState.Detached;
                    if (transaction != null) await transaction.RollbackAsync(ct);
                    continue;
                }

                var outcome = await AdmitAsync(organizationId, app, plan.Value, approval, ct);
                if (!outcome.IsSuccess)
                {
                    if (transaction != null) await transaction.RollbackAsync(ct);
                    else await ReleaseAsync(organizationId, applicationId, previousStatus, ct);
                    return outcome.Error;
                }
                app.ApprovalClaimedUntilUtc = null;
                app.UpdatedAtUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
                if (transaction != null) await transaction.CommitAsync(ct);
                await AnnounceAsync(organizationId, app, outcome.Value.MemberCreated, approval.UserId, ct);
                return await AnswerAsync(organizationId, app, outcome.Value with { Approved = true }, ct);
            }
            finally
            {
                if (transaction != null) await transaction.DisposeAsync();
            }
        }
        return DomainErrors.Application.Approving;
    }

    /// <summary>
    /// The workspace's rules for an approved application, and for the member it made, once the
    /// approval is committed. Only the approval that made the claim gets here, so a retry or a
    /// second reviewer sets nothing off again. A rule that fails cannot undo the approval.
    /// </summary>
    private async Task AnnounceAsync(Guid organizationId, ClubApplication app, bool memberCreated, Guid userId, CancellationToken ct)
    {
        if (_automations == null || app.MemberId is not Guid memberId) return;
        var name = $"{app.FirstName} {app.LastName}".Trim();
        try
        {
            await _automations.DispatchAsync(AutomationCatalog.ApplicationApproved, new AutomationEvent(
                organizationId, userId, app.Id.ToString(), name, Email: app.Email, MemberId: memberId, RecordId: app.Id), ct);
            if (memberCreated)
                await _automations.DispatchAsync(AutomationCatalog.MemberCreated, new AutomationEvent(
                    organizationId, userId, memberId.ToString(), name, Email: app.Email, MemberId: memberId), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Application {ApplicationId} was approved, but the workspace's automations for it failed", app.Id);
        }
    }

    /// <summary>What an approval will do, worked out before the claim so a refusal writes nothing.</summary>
    private sealed record Plan(CatalogItemDto Tier, ClubMember? Existing, ClubHousehold? Household, string HouseholdRole, bool FeeApplies, int FeeDueDays,
        bool HouseholdGone = false);

    private async Task<Result<Plan>> PlanAsync(Guid organizationId, ClubApplication app, Approval approval, CancellationToken ct)
    {
        var request = approval.Request;
        var email = (app.Email ?? "").Trim();
        // The member the sales side already made for them (a deal's handoff), else the one
        // member with the applicant's address and name: the same person. Someone else at a
        // shared address (a couple with one mailbox) is a different person, made a member.
        var name = $"{app.FirstName} {app.LastName}";
        var existing = await LinkedMemberAsync(organizationId, app, approval, ct) ?? (email.Length == 0 ? null : (await _db.ClubMembers
                .Where(m => m.OrganizationId == organizationId && m.Email.ToLower() == email.ToLower())
                .ToListAsync(ct))
            .Where(m => SameName($"{m.FirstName} {m.LastName}", name))
            .OrderBy(m => m.JoinedAtUtc).FirstOrDefault());

        // The tier: the one asked for at approval, else the application's; one of the club's, on sale.
        var tierName = Trimmed(request.Tier) ?? (existing?.Tier is { } kept && string.IsNullOrWhiteSpace(app.RequestedTier) ? kept : app.RequestedTier);
        if (string.IsNullOrWhiteSpace(tierName)) return DomainErrors.Club.TierUnknown("(none)");
        var tier = await _catalog.TierAsync(organizationId, tierName, ct);
        if (tier == null) return DomainErrors.Club.TierUnknown(tierName.Trim());
        if (!tier.IsActive && existing == null) return DomainErrors.Club.TierNotOnSale(tier.Name);
        var currency = await _catalog.CurrencyAsync(organizationId, ct);
        if (approval.MayHandleMoney && !string.Equals(tier.Currency, currency, StringComparison.OrdinalIgnoreCase)
            && (tier.Price > 0 || tier.InitiationFee > 0))
            return DomainErrors.Club.CurrencyMismatch(tier.Currency, currency);

        ClubHousehold? household = null;
        var householdGone = false;
        var role = HouseholdRoles.Normalize(request.HouseholdRole ?? app.HouseholdRole) ?? HouseholdRoles.Other;
        if (role == HouseholdRoles.Primary) return DomainErrors.Application.HouseholdRoleInvalid;
        if ((request.HouseholdId ?? app.HouseholdId) is { } householdId)
        {
            household = await _db.ClubHouseholds.AsNoTracking().FirstOrDefaultAsync(h => h.Id == householdId && h.OrganizationId == organizationId, ct);
            // A household named on this approval has to be there. One the application named that
            // has gone since (deleted, or the member who headed it with it) is no reason the
            // applicant cannot be admitted, and nothing else can take it off the application.
            if (household == null && request.HouseholdId != null) return DomainErrors.Household.NotFound;
            householdGone = household == null;
        }
        var rules = await _rules.ReadAsync(organizationId, ct);
        // A member who was never more than Pending (the old approval left them so) pays the fee
        // of joining; someone who was a member before does not pay it again.
        var feeApplies = existing == null || existing.Status == "Pending";
        return new Plan(tier, existing, household, role, feeApplies, rules.Billing.PaymentTermsDays, householdGone);
    }

    /// <summary>
    /// The member the sales side already made for the applicant, if it did. A deal's welcome
    /// handoff adds the deal's contact to the club's members as pending (OnboardingService),
    /// linked to the deal, its contact and its lead, often before the contract is countersigned
    /// or the application approved; that member is the one admitted, never a second one beside
    /// them, whatever address or spelling the application carries.
    /// </summary>
    /// <remarks>
    /// Found by the application's lead or contact (and the contact or lead each became), by the
    /// deal its lead became or the deal of the contract it is approved for, or as the member the
    /// deal's handoff named. Held to the applicant's name or address as well, so the other half
    /// of a couple on the same deal stays a person of their own; and a public-form application,
    /// linked to the sales side by its address alone, to the name.
    /// </remarks>
    private async Task<ClubMember?> LinkedMemberAsync(Guid organizationId, ClubApplication app, Approval approval, CancellationToken ct)
    {
        var leadIds = new List<Guid>();
        var contactIds = new List<Guid>();
        var dealIds = new List<Guid>();
        if (app.LeadId is { } lead) leadIds.Add(lead);
        if (app.ContactId is { } contact)
        {
            contactIds.Add(contact);
            if (await _db.Contacts.AsNoTracking().Where(c => c.Id == contact && c.OrganizationId == organizationId)
                    .Select(c => c.ConvertedFromLeadId).FirstOrDefaultAsync(ct) is { } fromLead)
                leadIds.Add(fromLead);
        }
        if (leadIds.Count > 0)
        {
            var became = await _db.Leads.AsNoTracking().Where(l => l.OrganizationId == organizationId && leadIds.Contains(l.Id))
                .Select(l => new { l.ConvertedToContactId, l.ConvertedToDealId }).ToListAsync(ct);
            contactIds.AddRange(became.Where(l => l.ConvertedToContactId != null).Select(l => l.ConvertedToContactId!.Value));
            dealIds.AddRange(became.Where(l => l.ConvertedToDealId != null).Select(l => l.ConvertedToDealId!.Value));
        }
        var contractIds = new[] { approval.ContractId, app.ContractId }.OfType<Guid>().Distinct().ToList();
        if (contractIds.Count > 0)
            dealIds.AddRange(await _db.Contracts.AsNoTracking()
                .Where(c => c.OrganizationId == organizationId && contractIds.Contains(c.Id) && c.DealId != null)
                .Select(c => c.DealId!.Value).ToListAsync(ct));
        if (leadIds.Count + contactIds.Count + dealIds.Count == 0) return null;

        var handedOver = dealIds.Count == 0 ? [] : await _db.DealOnboardings.AsNoTracking()
            .Where(o => o.OrganizationId == organizationId && dealIds.Contains(o.DealId) && o.ClubMemberId != null)
            .Select(o => o.ClubMemberId!.Value).ToListAsync(ct);
        var linked = await _db.ClubMembers
            .Where(m => m.OrganizationId == organizationId && m.ErasedAtUtc == null
                && ((m.LeadId != null && leadIds.Contains(m.LeadId.Value))
                    || (m.ContactId != null && contactIds.Contains(m.ContactId.Value))
                    || (m.DealId != null && dealIds.Contains(m.DealId.Value))
                    || handedOver.Contains(m.Id)))
            .ToListAsync(ct);
        var name = $"{app.FirstName} {app.LastName}";
        var email = (app.Email ?? "").Trim();
        var byAddressToo = app.Source != ApplicationSources.PublicForm;
        return linked
            .Select(m => (Member: m, Named: SameName($"{m.FirstName} {m.LastName}", name)))
            .Where(x => x.Named || (byAddressToo && email.Length > 0 && string.Equals((x.Member.Email ?? "").Trim(), email, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(x => x.Named)
            .ThenBy(x => x.Member.JoinedAtUtc)
            .Select(x => x.Member)
            .FirstOrDefault();
    }

    /// <summary>What the approval did, for the answer.</summary>
    private sealed record Outcome
    {
        public bool MemberCreated { get; init; }
        public bool Approved { get; init; }
        public bool DuesStarted { get; init; }
        public bool FeeCharged { get; init; }
        public bool JoinedHousehold { get; init; }
        public bool WelcomeQueued { get; init; }
        public List<string> Notes { get; init; } = new();
    }

    /// <summary>Everything after the claim: the member, their links, household and consent, the money and the welcome.</summary>
    private async Task<Result<Outcome>> AdmitAsync(Guid organizationId, ClubApplication app, Plan plan, Approval approval, CancellationToken ct)
    {
        var request = approval.Request;
        var now = DateTime.UtcNow;
        var notes = new List<string>();
        var memberId = app.MemberId!.Value;
        var member = await _db.ClubMembers.FirstOrDefaultAsync(m => m.Id == memberId && m.OrganizationId == organizationId, ct);
        var created = false;
        var readmitted = false;
        if (member == null)
        {
            member = new ClubMember
            {
                Id = memberId,
                OrganizationId = organizationId,
                OwnerUserId = approval.UserId,
                FirstName = app.FirstName,
                LastName = app.LastName,
                Email = app.Email,
                Phone = app.Phone,
                Tier = plan.Tier.Name,
                // Approved is admitted: the dues start now, and the billing lapses a member who
                // does not pay them. "Pending until the first dues" had nothing to end it.
                Status = "Active",
                JoinedAtUtc = now,
                RenewsAtUtc = now.AddYears(1),
                DuesAmount = plan.Tier.Price,
                DuesFrequency = Cadence(plan.Tier.Cadence),
                Notes = app.ReasonForJoining,
                // The language they applied in: the welcome, statements and reminders follow it.
                PreferredLanguage = AppLanguages.Normalise(app.Language),
            };
            _db.ClubMembers.Add(member);
            created = true;
        }
        else if (member.Status is "Pending" or "Lapsed" or "Cancelled")
        {
            // Approving someone who was a member is admitting them again, at the tier approved
            // and its dues: the dues started below are that tier's, so the record says so too.
            member.Status = "Active";
            member.BillingLapsedAtUtc = null;
            member.StatusBeforeLapse = null;
            member.Tier = plan.Tier.Name;
            member.DuesAmount = plan.Tier.Price;
            member.DuesFrequency = Cadence(plan.Tier.Cadence);
            readmitted = true;
            notes.Add($"{Name(member)} was already a member, so their record was used and set active again.");
        }
        else notes.Add($"{Name(member)} is already an active member: the application is linked to their record, their tier stays {member.Tier}, and nothing was charged or started.");
        // A member the club had already keeps the language staff set; one with none takes the one they applied in.
        if (member.PreferredLanguage == null && AppLanguages.Normalise(app.Language) is { } applied) member.PreferredLanguage = applied;

        // The person the sales side has for them, carried onto the member. From the public form
        // the link is by address alone, so it is carried only when the name matches as well.
        var trusted = app.Source != ApplicationSources.PublicForm;
        if (member.ContactId == null && app.ContactId is { } contactId)
        {
            var contact = await _db.Contacts.AsNoTracking().Where(c => c.Id == contactId && c.OrganizationId == organizationId)
                .Select(c => new { c.Name, c.ConvertedFromLeadId }).FirstOrDefaultAsync(ct);
            if (contact != null && (trusted || SameName(contact.Name, Name(member))))
                member.ContactId = contactId;
            else if (contact != null)
                notes.Add($"The contact with this address is {contact.Name}, so it was not linked to {Name(member)}.");
        }
        if (member.LeadId == null && app.LeadId is { } leadId)
        {
            var lead = await _db.Leads.AsNoTracking().Where(l => l.Id == leadId && l.OrganizationId == organizationId).Select(l => l.Name).FirstOrDefaultAsync(ct);
            if (lead != null && (trusted || SameName(lead, Name(member)))) member.LeadId = leadId;
        }
        if (member.LeadId == null && member.ContactId is { } linkedContact)
            member.LeadId = await _db.Contacts.AsNoTracking().Where(c => c.Id == linkedContact).Select(c => c.ConvertedFromLeadId).FirstOrDefaultAsync(ct);
        // The deal they were sold on: the contract's, else the one their lead became. The member's
        // page links to it, and the deal's page to them, as a deal's handoff links them.
        if (member.DealId == null && (approval.ContractId ?? app.ContractId) is { } signed)
            member.DealId = await _db.Contracts.AsNoTracking().Where(c => c.Id == signed && c.OrganizationId == organizationId)
                .Select(c => c.DealId).FirstOrDefaultAsync(ct);
        if (member.DealId == null && member.LeadId is { } memberLead)
            member.DealId = await _db.Leads.AsNoTracking().Where(l => l.Id == memberLead && l.OrganizationId == organizationId)
                .Select(l => l.ConvertedToDealId).FirstOrDefaultAsync(ct);
        await _db.SaveChangesAsync(ct);
        if (created)
        {
            // The house account opens with the member, as it does for one added by hand.
            var opened = await _ledger.OpenWithBalanceAsync(organizationId, member.Id, 0, ct);
            if (!opened.IsSuccess) return opened.Error;
        }

        if (plan.HouseholdGone) notes.Add("The household the application named no longer exists, so they were not added to one.");
        var joined = await JoinHouseholdAsync(organizationId, member, plan, approval, notes, ct);
        await CarryConsentAsync(organizationId, app, member, ct);

        // The money: the tier's dues and its initiation fee, when whoever approves may charge accounts.
        // Whether the fee is due is decided here, once, and kept: dues finished later by someone
        // who may charge accounts must not charge a returning member the fee this approval waived.
        app.ReturningMember = !plan.FeeApplies;
        var duesStarted = false;
        var feeCharged = false;
        var wantDues = request.StartDues != false;
        var wantFee = request.ChargeInitiationFee != false;
        // An active member's membership does not change: no dues at the tier they asked for
        // (their tier stays theirs) and no fee. Starting dues for them billed a Silver member
        // Gold dues while their record still said Silver.
        var admitting = created || readmitted;
        if (admitting && approval.MayHandleMoney)
        {
            var money = await SetUpMoneyAsync(organizationId, app, member, plan.Tier, plan.FeeApplies, wantDues, wantFee, plan.FeeDueDays, notes, ct);
            if (!money.IsSuccess) return money.Error;
            (duesStarted, feeCharged) = money.Value;
            app.MoneyPending = false;
        }
        else if (admitting && ((wantDues && plan.Tier.Price > 0 && !await HasRunningSubscriptionAsync(organizationId, member.Id, ct))
                 || (wantFee && plan.FeeApplies && plan.Tier.InitiationFee > 0)))
        {
            // Left for finance only when there is something to set up: a member whose dues
            // already run, with no fee due, has nothing waiting.
            app.MoneyPending = true;
            notes.Add("The dues and the initiation fee are left for someone who can charge accounts: they finish it by approving again.");
        }

        var welcomed = false;
        if (request.SendWelcome != false && app.WelcomeMessageId == null && (created || readmitted))
            welcomed = await WelcomeAsync(organizationId, app, member, plan.Tier, approval.UserId, notes, ct);

        return new Outcome
        {
            MemberCreated = created,
            DuesStarted = duesStarted,
            FeeCharged = feeCharged,
            JoinedHousehold = joined,
            WelcomeQueued = welcomed,
            Notes = notes,
        };
    }

    /// <summary>Finishes an approval whose dues and fee were left for someone who may charge accounts.</summary>
    private async Task<Result<ApprovalResultDto>?> FinishMoneyAsync(Guid organizationId, ClubApplication app, Approval approval, CancellationToken ct)
    {
        var member = app.MemberId is { } id
            ? await _db.ClubMembers.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id && m.OrganizationId == organizationId, ct)
            : null;
        if (member == null) return DomainErrors.Application.MemberGone;
        var tier = await _catalog.TierAsync(organizationId, member.Tier, ct);
        if (tier == null) return DomainErrors.Club.TierUnknown(member.Tier);
        var currency = await _catalog.CurrencyAsync(organizationId, ct);
        if (!string.Equals(tier.Currency, currency, StringComparison.OrdinalIgnoreCase)) return DomainErrors.Club.CurrencyMismatch(tier.Currency, currency);
        var rules = await _rules.ReadAsync(organizationId, ct);

        var transaction = _db.Database.IsRelational() && _db.Database.CurrentTransaction == null
            ? await _db.Database.BeginTransactionAsync(ct)
            : null;
        try
        {
            app.ApprovalClaimedUntilUtc = DateTime.UtcNow + ClaimFor;
            app.UpdatedAtUtc = DateTime.UtcNow;
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                _db.Entry(app).State = EntityState.Detached;
                if (transaction != null) await transaction.RollbackAsync(ct);
                return null;
            }
            var notes = new List<string>();
            var request = approval.Request;
            // The fee as the approval decided it: a member who had been one before pays no joining fee.
            var money = await SetUpMoneyAsync(organizationId, app, member, tier,
                feeApplies: !app.ReturningMember, request.StartDues != false, request.ChargeInitiationFee != false, rules.Billing.PaymentTermsDays, notes, ct);
            if (!money.IsSuccess)
            {
                if (transaction != null) await transaction.RollbackAsync(ct);
                else await ReleaseAsync(organizationId, app.Id, ApplicationStatuses.Approved, ct);
                return money.Error;
            }
            app.MoneyPending = false;
            // A paid deposit that finished the money is the payment that did it, as it is when it approves.
            if (approval.DepositPaymentId is { } depositPaid && app.DepositPaymentId == null) app.DepositPaymentId = depositPaid;
            app.ApprovalClaimedUntilUtc = null;
            app.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            if (transaction != null) await transaction.CommitAsync(ct);
            return await AnswerAsync(organizationId, app, new Outcome
            {
                Approved = true, DuesStarted = money.Value.DuesStarted, FeeCharged = money.Value.FeeCharged, Notes = notes,
            }, ct);
        }
        finally
        {
            if (transaction != null) await transaction.DisposeAsync();
        }
    }

    /// <summary>The dues subscription and the initiation fee, each at most once for the application.</summary>
    private async Task<Result<(bool DuesStarted, bool FeeCharged)>> SetUpMoneyAsync(Guid organizationId, ClubApplication app, ClubMember member,
        CatalogItemDto tier, bool feeApplies, bool wantDues, bool wantFee, int feeDueDays, List<string> notes, CancellationToken ct)
    {
        var duesStarted = false;
        var feeCharged = false;
        if (wantDues && app.DuesSubscriptionId == null && tier.Price > 0)
        {
            if (await HasRunningSubscriptionAsync(organizationId, member.Id, ct))
                notes.Add($"{Name(member)} already has a subscription running, so no second one was started.");
            else
            {
                var started = await _ledger.CreateSubscriptionAsync(organizationId, new SubscriptionWriteRequest
                {
                    MemberId = member.Id,
                    Name = $"{tier.Name} dues",
                    Amount = tier.Price,
                    Cadence = Cadence(tier.Cadence),
                    CatalogItemId = tier.Id != Guid.Empty ? tier.Id : null,
                }, ct);
                if (!started.IsSuccess) return started.Error;
                app.DuesSubscriptionId = started.Value.Id;
                duesStarted = true;
            }
        }
        if (wantFee && app.InitiationChargeId == null && tier.InitiationFee is { } fee && fee > 0)
        {
            if (!feeApplies) notes.Add("No initiation fee was charged: they were a member before.");
            else
            {
                // Once per member, whatever retries: the ledger hands a second post with this key the first charge back.
                var posted = await _ledger.PostChargeAsync(organizationId, new PostChargeRequest
                {
                    MemberId = member.Id,
                    Kind = "Dues",
                    Description = $"Initiation fee, {tier.Name}",
                    Amount = fee,
                    VatRate = tier.VatRate,
                    DueAtUtc = DateTime.UtcNow.AddDays(feeDueDays).ToString("o", CultureInfo.InvariantCulture),
                }, $"initiation:{member.Id}", ct);
                if (!posted.IsSuccess) return posted.Error;
                app.InitiationChargeId = posted.Value.Id;
                feeCharged = true;
            }
        }
        return (duesStarted, feeCharged);
    }

    /// <summary>Whether the member already has dues running (a subscription not cancelled): a second is never started.</summary>
    private Task<bool> HasRunningSubscriptionAsync(Guid organizationId, Guid memberId, CancellationToken ct) =>
        _db.ClubSubscriptions.AsNoTracking()
            .AnyAsync(s => s.OrganizationId == organizationId && s.MemberId == memberId && s.Status != "Cancelled", ct);

    /// <summary>
    /// Puts the new member in their household: the one they were already in as a contact (a couple
    /// recorded on the sales side), else the one the application names. Answers whether they joined.
    /// </summary>
    private async Task<bool> JoinHouseholdAsync(Guid organizationId, ClubMember member, Plan plan, Approval approval, List<string> notes, CancellationToken ct)
    {
        var mine = await _db.ClubHouseholdMembers.AnyAsync(l => l.OrganizationId == organizationId && l.MemberId == member.Id, ct);
        if (mine)
        {
            if (plan.Household != null) notes.Add($"{Name(member)} is already in a household, so they were not added to the {plan.Household.Name} household.");
            return false;
        }
        // Their place in a household as a contact becomes their place as the member.
        if (member.ContactId is { } contactId)
        {
            var asContact = await _db.ClubHouseholdMembers.FirstOrDefaultAsync(l => l.OrganizationId == organizationId && l.ContactId == contactId, ct);
            if (asContact != null)
            {
                asContact.MemberId = member.Id;
                asContact.ContactId = null;
                asContact.UpdatedAtUtc = DateTime.UtcNow;
                await TouchHouseholdAsync(organizationId, asContact.HouseholdId, ct);
                await _db.SaveChangesAsync(ct);
                return true;
            }
        }
        if (plan.Household == null) return false;
        var links = await _db.ClubHouseholdMembers.AsNoTracking()
            .Where(l => l.OrganizationId == organizationId && l.HouseholdId == plan.Household.Id).ToListAsync(ct);
        var role = plan.HouseholdRole;
        if (HouseholdRoles.IsCouple(role) && links.Any(l => HouseholdRoles.IsCouple(l.Role)))
        {
            notes.Add($"The {plan.Household.Name} household already has a spouse or partner, so {Name(member)} joined it as Other.");
            role = HouseholdRoles.Other;
        }
        var rollUp = approval.Request.RollUpBilling == true;
        if (rollUp && !approval.MayHandleMoney)
        {
            notes.Add("Roll-up billing was not switched on: that needs someone who can charge accounts.");
            rollUp = false;
        }
        if (rollUp && links.FirstOrDefault(l => l.IsPrimary)?.MemberId == null)
        {
            notes.Add("Roll-up billing was not switched on: the household's primary is not a member.");
            rollUp = false;
        }
        _db.ClubHouseholdMembers.Add(new ClubHouseholdMember
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            HouseholdId = plan.Household.Id,
            MemberId = member.Id,
            Role = role,
            RollUpBilling = rollUp,
            Source = HouseholdLinkSources.Club,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = approval.UserId,
        });
        await TouchHouseholdAsync(organizationId, plan.Household.Id, ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task TouchHouseholdAsync(Guid organizationId, Guid householdId, CancellationToken ct)
    {
        var household = await _db.ClubHouseholds.FirstOrDefaultAsync(h => h.Id == householdId && h.OrganizationId == organizationId, ct);
        if (household != null) household.UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// The consent the applicant gave on the form, as the member's: each answer with where and
    /// when it was given, unless the member has a later answer of their own for it.
    /// </summary>
    private async Task CarryConsentAsync(Guid organizationId, ClubApplication app, ClubMember member, CancellationToken ct)
    {
        var given = await _consent.CurrentAsync(organizationId, ConsentSubjects.Application, [app.Id], ct: ct);
        if (given.Count == 0) return;
        var standing = (await _consent.CurrentAsync(organizationId, ConsentSubjects.Member, [member.Id], ct: ct))
            .ToDictionary(c => (c.Channel, c.Purpose));
        foreach (var answer in given)
        {
            if (standing.TryGetValue((answer.Channel, answer.Purpose), out var mine) && mine.RecordedAtUtc >= answer.RecordedAtUtc) continue;
            await _consent.AddCurrentAsync(new ConsentRecord
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                SubjectType = ConsentSubjects.Member,
                SubjectId = member.Id,
                Channel = answer.Channel,
                Purpose = answer.Purpose,
                Granted = answer.Granted,
                Source = answer.Source,
                Address = answer.Address,
                Detail = Clip($"{answer.Detail} (given on the membership application)".Trim(), 500),
                RecordedAtUtc = answer.RecordedAtUtc,
                RecordedByUserId = answer.RecordedByUserId,
            }, ct);
        }
    }

    /// <summary>Queues the welcome email on the outbox (a service message) and logs it on the member.</summary>
    private async Task<bool> WelcomeAsync(Guid organizationId, ClubApplication app, ClubMember member, CatalogItemDto tier, Guid userId, List<string> notes, CancellationToken ct)
    {
        var address = (member.Email ?? "").Trim();
        var workspace = await _db.Organizations.AsNoTracking().Where(o => o.Id == organizationId).Select(o => new { o.Name, o.Locale }).FirstOrDefaultAsync(ct);
        // In the member's language: the one they applied in, or staff set (English when none).
        var language = AppLanguages.ForMember(member.PreferredLanguage);
        var club = workspace?.Name ?? MemberTexts.T(language, "theClub");
        if (!ValidationHelper.IsValidEmail(address))
        {
            notes.Add($"No welcome email: {Name(member)} has no valid email address.");
            return false;
        }
        var subject = MemberTexts.T(language, "welcome.subject", ("club", club));
        var body = new StringBuilder()
            .Append(MemberTexts.T(language, "greeting", ("name", string.IsNullOrWhiteSpace(member.FirstName) ? Name(member) : member.FirstName.Trim()))).Append("\n\n")
            .Append(MemberTexts.T(language, "welcome.approved", ("tier", tier.Name), ("club", club))).Append("\n\n")
            .Append(MemberTexts.T(language, "welcome.closing")).Append("\n\n")
            .Append(club)
            .ToString();
        var log = new MemberMessage
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            MemberId = member.Id,
            MemberName = Name(member),
            Email = address,
            Channel = CampaignChannels.Email,
            Category = "Other",
            Purpose = ConsentPurposes.Service,
            Subject = subject,
            Preview = Clip(body, 500),
            Status = MemberMessageStatuses.Queued,
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.MemberMessages.Add(log);
        var queued = await _outbox.QueueEmailAsync(new OutboundEmailRequest
        {
            OrganizationId = organizationId,
            Source = OutboundSources.Welcome,
            SourceId = app.Id,
            To = [address],
            RecipientName = Name(member),
            Subject = subject,
            Body = body,
            CreatedByUserId = userId,
            ReplyToUserId = userId,
            MemberId = member.Id,
            Purpose = ConsentPurposes.Service,
        }, ct);
        if (queued.IsSuccess)
        {
            log.OutboundMessageId = queued.Value.Id;
            app.WelcomeMessageId = queued.Value.Id;
        }
        else
        {
            log.Status = MemberMessageStatuses.Suppressed;
            log.Reason = Clip(queued.Error.Description, 500);
            notes.Add($"No welcome email: {queued.Error.Description}");
        }
        await _db.SaveChangesAsync(ct);
        return queued.IsSuccess;
    }

    /// <summary>Where there is no transaction to roll back: lets go of a claim whose approval was refused part-way.</summary>
    private async Task ReleaseAsync(Guid organizationId, Guid applicationId, string previousStatus, CancellationToken ct)
    {
        foreach (var entry in _db.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList())
            entry.State = EntityState.Detached;
        var app = await TrackedAsync(organizationId, applicationId, ct);
        if (app == null) return;
        if (previousStatus != ApplicationStatuses.Approved)
        {
            app.Status = previousStatus;
            app.ApprovedAtUtc = null;
            app.ApprovedByUserId = null;
            app.ApprovalTrigger = null;
        }
        app.ApprovalClaimedUntilUtc = null;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<Result<ApprovalResultDto>> AnswerAsync(Guid organizationId, ClubApplication app, Outcome outcome, CancellationToken ct)
    {
        var dto = (await MapAsync(organizationId, [app], ct))[0];
        return new ApprovalResultDto
        {
            Application = dto,
            MemberId = app.MemberId ?? Guid.Empty,
            MemberName = dto.MemberName ?? $"{app.FirstName} {app.LastName}".Trim(),
            MemberCreated = outcome.MemberCreated,
            Approved = outcome.Approved,
            DuesStarted = outcome.DuesStarted,
            FeeCharged = outcome.FeeCharged,
            JoinedHousehold = outcome.JoinedHousehold,
            WelcomeQueued = outcome.WelcomeQueued,
            MoneyPending = app.MoneyPending,
            Notes = outcome.Notes,
        };
    }

    // ── the public form ──────────────────────────────────────────────────────

    public async Task<ApplicationFormDto?> FormAsync(Guid organizationId, CancellationToken ct = default)
    {
        var form = await _db.ClubApplicationForms.AsNoTracking().FirstOrDefaultAsync(f => f.OrganizationId == organizationId, ct);
        return form == null ? null : ToDto(form);
    }

    public async Task<Result<ApplicationFormDto>> SaveFormAsync(Guid organizationId, Guid userId, ApplicationFormRequest request, CancellationToken ct = default)
    {
        var intro = Trimmed(request.Intro);
        var success = Trimmed(request.SuccessMessage);
        var consent = Trimmed(request.ConsentText);
        if (intro?.Length > 1000 || success?.Length > 500 || consent?.Length > 500) return DomainErrors.Application.FormTextTooLong;
        var form = await _db.ClubApplicationForms.FirstOrDefaultAsync(f => f.OrganizationId == organizationId, ct);
        if (form == null)
        {
            form = new ClubApplicationForm
            {
                Id = Guid.NewGuid(), OrganizationId = organizationId, PublicToken = NewToken(), IsActive = true,
                CreatedByUserId = userId, CreatedAtUtc = DateTime.UtcNow,
            };
            _db.ClubApplicationForms.Add(form);
        }
        if (request.IsActive is { } active) form.IsActive = active;
        if (request.Intro != null) form.Intro = intro;
        if (request.SuccessMessage != null) form.SuccessMessage = success;
        if (request.ConsentText != null) form.ConsentText = consent;
        form.UpdatedAtUtc = DateTime.UtcNow;
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Set up by someone else at the same moment: theirs stands.
            _db.Entry(form).State = EntityState.Detached;
            form = await _db.ClubApplicationForms.AsNoTracking().FirstAsync(f => f.OrganizationId == organizationId, ct);
        }
        return ToDto(form);
    }

    public async Task<Result<ApplicationFormDto>> RotateFormAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        var form = await _db.ClubApplicationForms.FirstOrDefaultAsync(f => f.OrganizationId == organizationId, ct);
        if (form == null) return await SaveFormAsync(organizationId, userId, new ApplicationFormRequest(), ct);
        form.PublicToken = NewToken();
        form.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(form);
    }

    public async Task<PublicApplicationFormDto?> PublicFormAsync(string token, string? language = null, CancellationToken ct = default)
    {
        var form = await FormByTokenAsync(token, ct);
        if (form?.OrganizationId is not { } organizationId) return null;
        using var acting = _tenant.ActFor(organizationId, form.CreatedByUserId);
        var workspace = await _db.Organizations.AsNoTracking().Where(o => o.Id == organizationId).Select(o => new { o.Name, o.Locale }).FirstOrDefaultAsync(ct);
        var tiers = (await _catalog.TiersAsync(organizationId, ct)).Where(t => t.IsActive)
            .Select(t => new PublicTierDto(t.Name, t.Price, Cadence(t.Cadence), t.InitiationFee, t.Currency)).ToList();
        // The club's own words as it wrote them; the standard ones in the language the form is read in.
        var words = AppLanguages.ForMember(language);
        return new PublicApplicationFormDto(workspace?.Name ?? "", form.Intro, ConsentTextIn(form, words), tiers, await ConsentModeAsync(organizationId, ct),
            AppLanguages.Normalise(workspace?.Locale));
    }

    /// <summary>
    /// The words beside the marketing checkbox: the club's own, else the standard ones in
    /// <paramref name="language"/>. The wording dialog shows the English standard sentence when
    /// the club wrote none, and saving any change sends it back, so that sentence stored is read
    /// as the standard one, not as words the club wrote (as the capture form reads its default).
    /// </summary>
    private static string ConsentTextIn(ClubApplicationForm form, string language) =>
        string.IsNullOrWhiteSpace(form.ConsentText) || form.ConsentText.Trim() == DefaultConsentText
            ? MemberTexts.T(language, "form.applicationConsent")
            : form.ConsentText;

    public async Task<Result<PublicApplicationReceipt>> SubmitAsync(string token, PublicApplicationRequest request, CancellationToken ct = default)
    {
        var form = await FormByTokenAsync(token, ct);
        if (form?.OrganizationId is not { } organizationId) return DomainErrors.Application.FormNotFound;
        // Nobody signs in on the form: its link names the workspace, and everything here reads
        // and writes as that workspace, as its creator (whose the new lead is).
        using var acting = _tenant.ActFor(organizationId, form.CreatedByUserId);
        // The language the form was filled in: the thank-you is in it, the consent record keeps the
        // words shown in it, and the member the application becomes is written to in it. A post
        // that does not say (an old page, a club's own form) is answered in English, as before.
        var language = AppLanguages.ForMember(request.Language);
        var receipt = new PublicApplicationReceipt(string.IsNullOrWhiteSpace(form.SuccessMessage) ? MemberTexts.T(language, "form.applicationThanks") : form.SuccessMessage);

        // The hidden honeypot: a person never sees it, a bot fills it. Thanked, and nothing kept.
        if (!string.IsNullOrWhiteSpace(request.Website)) return receipt;

        var first = request.FirstName.Trim();
        var last = request.LastName.Trim();
        var email = request.Email.Trim();
        if (first.Length == 0 || last.Length == 0 || email.Length == 0) return DomainErrors.Application.NameRequired;
        // The workspace's privacy settings say whether the form asks for marketing consent at all,
        // and whether the box must be ticked (meant for a form that signs people up for news).
        var consentMode = await ConsentModeAsync(organizationId, ct);
        if (consentMode == MarketingConsentModes.Required && !request.MarketingConsent)
            return new Error("Application.ConsentRequired", MemberTexts.T(language, "form.consentRequired"));
        if (!ValidationHelper.IsValidEmail(email)) return DomainErrors.Application.EmailInvalid;
        var tiers = await _catalog.TiersAsync(organizationId, ct);
        var tier = tiers.FirstOrDefault(t => t.IsActive && string.Equals(t.Name, request.RequestedTier?.Trim(), StringComparison.OrdinalIgnoreCase));
        var name = $"{first} {last}";
        var summary = Summary(request, tier?.Name);

        // Linked to the person the sales side already has: the contact with this address, and
        // their open lead (which is told of the application, as the capture form tells it of an
        // inquiry); with neither, a new lead, made as the capture form makes one.
        var contact = await _db.Contacts.AsNoTracking()
            .Where(c => c.OrganizationId == organizationId && !c.IsArchived && c.Email.ToLower() == email.ToLower())
            .Select(c => new { c.Id, c.ConvertedFromLeadId }).FirstOrDefaultAsync(ct);
        Guid? leadId = await _leadOps.AttachInquiryAsync(organizationId, form.CreatedByUserId, email, LeadSource, summary, ct);
        leadId ??= contact?.ConvertedFromLeadId;
        Guid? contactId = contact?.Id;
        if (leadId == null && contact == null)
        {
            var created = await _leads.CreateAsync(form.CreatedByUserId, organizationId, new CreateLeadRequest
            {
                Name = name,
                Email = email,
                Phone = Trimmed(request.Phone),
                Status = "New",
                Source = LeadSource,
                Description = summary,
                AllowDuplicate = true,
            }, ct);
            if (!created.IsSuccess) return DomainErrors.Application.SubmitFailed;
            leadId = created.Value.Id;
            contactId = await _db.Leads.AsNoTracking().Where(l => l.Id == created.Value.Id).Select(l => l.ConvertedToContactId).FirstOrDefaultAsync(ct);
        }

        var now = DateTime.UtcNow;
        var row = new ClubApplication
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            FirstName = Clip(first, 100),
            LastName = Clip(last, 100),
            Email = Clip(email, 256),
            Phone = Trimmed(request.Phone) is { } phone ? Clip(phone, 64) : null,
            RequestedTier = tier?.Name ?? "",
            Occupation = Trimmed(request.Occupation) is { } occupation ? Clip(occupation, 200) : null,
            ReferredByName = Trimmed(request.ReferredByName) is { } referred ? Clip(referred, 200) : null,
            ReasonForJoining = Trimmed(request.ReasonForJoining) is { } reason ? Clip(reason, 2000) : null,
            Status = ApplicationStatuses.Submitted,
            Source = ApplicationSources.PublicForm,
            Language = AppLanguages.Normalise(request.Language),
            SubmittedAtUtc = now,
            LeadId = leadId,
            ContactId = contactId,
            CreatedAtUtc = now,
        };
        _db.ClubApplications.Add(row);
        await _db.SaveChangesAsync(ct);

        // Their answer to news and offers by email, as they gave it, with the words they were shown;
        // nothing when the form does not ask.
        if (consentMode != MarketingConsentModes.Off)
        {
            await _consent.AddCurrentAsync(new ConsentRecord
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                SubjectType = ConsentSubjects.Application,
                SubjectId = row.Id,
                Channel = ConsentChannels.Email,
                Purpose = ConsentPurposes.Marketing,
                Granted = request.MarketingConsent,
                Source = ConsentSources.Form,
                Address = email.ToLowerInvariant(),
                Detail = Clip($"Membership application form: \"{ConsentTextIn(form, language)}\"", 500),
                RecordedAtUtc = now,
            }, ct);
        }

        await _notifications.CreateAsync(form.CreatedByUserId, organizationId, new CreateNotificationRequest(
            "club_application",
            "A new membership application",
            $"{name} applied{(tier != null ? $" for {tier.Name}" : "")} on the application form.",
            "/applications"), ct);
        return receipt;
    }

    private async Task<ClubApplicationForm?> FormByTokenAsync(string token, CancellationToken ct)
    {
        var t = (token ?? "").Trim();
        if (t.Length == 0 || t.Length > 64) return null;
        // An anonymous request has no workspace: the form is found by its token across all of
        // them, and only an open one answers.
        return await _db.ClubApplicationForms.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(f => f.PublicToken == t && f.IsActive, ct);
    }

    private static ApplicationFormDto ToDto(ClubApplicationForm form) => new(
        form.Id, form.PublicToken, $"/apply/{form.PublicToken}", form.IsActive, form.Intro, form.SuccessMessage,
        form.ConsentText ?? DefaultConsentText, form.CreatedAtUtc);

    private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    private static string Summary(PublicApplicationRequest r, string? tier)
    {
        var lines = new List<string> { $"Name: {r.FirstName.Trim()} {r.LastName.Trim()}", $"Email: {r.Email.Trim()}" };
        if (tier != null) lines.Add($"Tier asked for: {tier}");
        if (!string.IsNullOrWhiteSpace(r.Phone)) lines.Add($"Phone: {r.Phone.Trim()}");
        if (!string.IsNullOrWhiteSpace(r.Occupation)) lines.Add($"Occupation: {r.Occupation.Trim()}");
        if (!string.IsNullOrWhiteSpace(r.ReferredByName)) lines.Add($"Referred by: {r.ReferredByName.Trim()}");
        return string.Join("\n", lines);
    }

    // ── shared ───────────────────────────────────────────────────────────────

    /// <summary>The application, tracked and read fresh: a copy tracked by an earlier attempt in this request is let go first.</summary>
    private async Task<ClubApplication?> TrackedAsync(Guid organizationId, Guid applicationId, CancellationToken ct)
    {
        foreach (var entry in _db.ChangeTracker.Entries<ClubApplication>().Where(e => e.Entity.Id == applicationId).ToList())
            entry.State = EntityState.Detached;
        return await _db.ClubApplications.FirstOrDefaultAsync(a => a.Id == applicationId && a.OrganizationId == organizationId, ct);
    }

    private async Task<Result<ApplicationDto>> SaveAndMapAsync(Guid organizationId, ClubApplication row, CancellationToken ct)
    {
        row.UpdatedAtUtc = DateTime.UtcNow;
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.Entry(row).State = EntityState.Detached;
            return DomainErrors.Application.Changed;
        }
        return (await MapAsync(organizationId, [row], ct))[0];
    }

    /// <summary>Moves the application through review; null when it moved, else why not.</summary>
    private static Error? Move(ClubApplication row, string status, Guid userId, string? notes)
    {
        var to = ApplicationStatuses.All.FirstOrDefault(s => string.Equals(s, status, StringComparison.OrdinalIgnoreCase));
        if (to == null || to == ApplicationStatuses.Approved) return DomainErrors.Application.StatusChange(row.Status, status.Length == 0 ? "nothing" : status);
        if (to == row.Status)
        {
            if (notes != null) row.ReviewerNotes = notes;
            return null;
        }
        var allowed = row.Status switch
        {
            ApplicationStatuses.Submitted => to is ApplicationStatuses.UnderReview or ApplicationStatuses.Waitlisted or ApplicationStatuses.Rejected,
            ApplicationStatuses.UnderReview => to is ApplicationStatuses.Submitted or ApplicationStatuses.Waitlisted or ApplicationStatuses.Rejected,
            // A waitlisted applicant can be reviewed again or reopened when a place frees up.
            ApplicationStatuses.Waitlisted => to is ApplicationStatuses.Submitted or ApplicationStatuses.UnderReview or ApplicationStatuses.Rejected,
            // A rejected one can be reopened when they apply again.
            ApplicationStatuses.Rejected => to is ApplicationStatuses.Submitted,
            _ => false,
        };
        if (!allowed) return DomainErrors.Application.StatusChange(row.Status, to);
        row.Status = to;
        if (to is ApplicationStatuses.Waitlisted or ApplicationStatuses.Rejected)
        {
            row.ReviewedAtUtc = DateTime.UtcNow;
            row.ReviewedByUserId = userId;
        }
        if (notes != null) row.ReviewerNotes = notes;
        return null;
    }

    /// <summary>Checks and copies the request's fields onto the row: on adding, the names and email are required; on changing, a field left out is kept.</summary>
    private async Task<Result> ApplyAsync(Guid organizationId, Guid userId, ClubApplication row, ApplicationWriteRequest r, bool creating, CancellationToken ct)
    {
        string Field(string? value, string current) => value == null ? current : value.Trim();
        var first = Field(r.FirstName, row.FirstName);
        var last = Field(r.LastName, row.LastName);
        var email = Field(r.Email, row.Email);
        if (first.Length == 0 || last.Length == 0 || email.Length == 0) return DomainErrors.Application.NameRequired;
        if (first.Length > 100) return DomainErrors.Application.TooLong("A first name", 100);
        if (last.Length > 100) return DomainErrors.Application.TooLong("A last name", 100);
        if (email.Length > 256) return DomainErrors.Application.TooLong("An email", 256);
        if (!ValidationHelper.IsValidEmail(email)) return DomainErrors.Application.EmailInvalid;
        if (r.Phone?.Trim().Length > 64) return DomainErrors.Application.TooLong("A phone number", 64);
        if (r.ReferredByName?.Trim().Length > 200) return DomainErrors.Application.TooLong("A referrer", 200);
        if (r.Occupation?.Trim().Length > 200) return DomainErrors.Application.TooLong("An occupation", 200);
        if (r.ReasonForJoining?.Trim().Length > 2000) return DomainErrors.Application.TooLong("The reason for joining", 2000);
        if (r.ReviewerNotes?.Trim().Length > 2000) return DomainErrors.Application.TooLong("The committee notes", 2000);

        if (r.RequestedTier != null || creating)
        {
            var tierName = (r.RequestedTier ?? "").Trim();
            if (tierName.Length > 0)
            {
                var tier = await _catalog.TierAsync(organizationId, tierName, ct);
                if (tier == null) return DomainErrors.Club.TierUnknown(tierName);
                if (!tier.IsActive && !string.Equals(tier.Name, row.RequestedTier, StringComparison.OrdinalIgnoreCase))
                    return DomainErrors.Club.TierNotOnSale(tier.Name);
                tierName = tier.Name;
            }
            row.RequestedTier = tierName;
        }
        // The lead and the contact are this workspace's, by the rule everything else reads them by.
        var links = await _records.CheckLinksAsync(userId, organizationId, new RecordLinks { LeadId = r.LeadId, ContactId = r.ContactId }, ct);
        if (!links.IsSuccess) return links.Error;
        if (r.HouseholdId is { } householdId
            && !await _db.ClubHouseholds.AnyAsync(h => h.Id == householdId && h.OrganizationId == organizationId, ct))
            return DomainErrors.Household.NotFound;
        if (r.HouseholdRole != null && r.HouseholdRole.Trim().Length > 0
            && HouseholdRoles.Normalize(r.HouseholdRole) is null or HouseholdRoles.Primary)
            return DomainErrors.Application.HouseholdRoleInvalid;

        row.FirstName = first;
        row.LastName = last;
        row.Email = email;
        if (r.Phone != null) row.Phone = Trimmed(r.Phone);
        if (r.ReferredByName != null) row.ReferredByName = Trimmed(r.ReferredByName);
        if (r.Occupation != null) row.Occupation = Trimmed(r.Occupation);
        if (r.ReasonForJoining != null) row.ReasonForJoining = Trimmed(r.ReasonForJoining);
        if (r.ReviewerNotes != null) row.ReviewerNotes = Trimmed(r.ReviewerNotes);
        if (r.LeadId != null) row.LeadId = r.LeadId;
        if (r.ContactId != null) row.ContactId = r.ContactId;
        if (r.HouseholdId != null) row.HouseholdId = r.HouseholdId;
        if (r.HouseholdRole != null) row.HouseholdRole = HouseholdRoles.Normalize(r.HouseholdRole);
        return Result.Success();
    }

    /// <summary>The contact (and its lead) with this address, when there is exactly one; with <paramref name="requireName"/>, only when it has this name too.</summary>
    private async Task<(Guid? ContactId, Guid? LeadId)> PersonByEmailAsync(Guid organizationId, string email, string name, bool requireName, CancellationToken ct)
    {
        var address = (email ?? "").Trim().ToLower();
        if (address.Length == 0) return (null, null);
        var contacts = await _db.Contacts.AsNoTracking()
            .Where(c => c.OrganizationId == organizationId && !c.IsArchived && c.Email.ToLower() == address)
            .Select(c => new { c.Id, c.Name, c.ConvertedFromLeadId }).Take(2).ToListAsync(ct);
        if (contacts.Count != 1) return (null, null);
        var contact = contacts[0];
        if (requireName && !SameName(contact.Name, name)) return (null, null);
        return (contact.Id, contact.ConvertedFromLeadId);
    }

    private async Task<List<ApplicationDto>> MapAsync(Guid organizationId, IReadOnlyList<ClubApplication> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return [];
        var leadIds = rows.Where(r => r.LeadId != null).Select(r => r.LeadId!.Value).Distinct().ToList();
        var contactIds = rows.Where(r => r.ContactId != null).Select(r => r.ContactId!.Value).Distinct().ToList();
        var memberIds = rows.Where(r => r.MemberId != null).Select(r => r.MemberId!.Value).Distinct().ToList();
        var householdIds = rows.Where(r => r.HouseholdId != null).Select(r => r.HouseholdId!.Value).Distinct().ToList();
        var leads = await _db.Leads.AsNoTracking().Where(l => l.OrganizationId == organizationId && leadIds.Contains(l.Id))
            .Select(l => new { l.Id, l.Name }).ToDictionaryAsync(l => l.Id, l => l.Name, ct);
        var contacts = await _db.Contacts.AsNoTracking().Where(c => c.OrganizationId == organizationId && contactIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name }).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var members = await _db.ClubMembers.AsNoTracking().Where(m => m.OrganizationId == organizationId && memberIds.Contains(m.Id))
            .Select(m => new { m.Id, m.FirstName, m.LastName }).ToDictionaryAsync(m => m.Id, m => $"{m.FirstName} {m.LastName}".Trim(), ct);
        var households = await _db.ClubHouseholds.AsNoTracking().Where(h => h.OrganizationId == organizationId && householdIds.Contains(h.Id))
            .Select(h => new { h.Id, h.Name }).ToDictionaryAsync(h => h.Id, h => h.Name, ct);
        var consent = (await _consent.CurrentAsync(organizationId, ConsentSubjects.Application, rows.Select(r => r.Id).ToList(),
                ConsentChannels.Email, ConsentPurposes.Marketing, ct))
            .ToDictionary(c => c.SubjectId, c => c.Granted ? ConsentStates.Granted : ConsentStates.Withdrawn);
        return rows.Select(r => new ApplicationDto
        {
            Id = r.Id,
            FirstName = r.FirstName,
            LastName = r.LastName,
            Email = r.Email,
            Phone = r.Phone,
            RequestedTier = r.RequestedTier,
            ReferredByName = r.ReferredByName,
            Occupation = r.Occupation,
            ReasonForJoining = r.ReasonForJoining,
            Status = r.Status,
            Source = r.Source,
            SubmittedAtUtc = r.SubmittedAtUtc,
            ReviewedAtUtc = r.ReviewedAtUtc,
            ReviewerNotes = r.ReviewerNotes,
            LeadId = r.LeadId is { } l && leads.ContainsKey(l) ? l : null,
            LeadName = r.LeadId is { } l2 ? leads.GetValueOrDefault(l2) : null,
            ContactId = r.ContactId is { } c && contacts.ContainsKey(c) ? c : null,
            ContactName = r.ContactId is { } c2 ? contacts.GetValueOrDefault(c2) : null,
            HouseholdId = r.HouseholdId is { } h && households.ContainsKey(h) ? h : null,
            HouseholdName = r.HouseholdId is { } h2 ? households.GetValueOrDefault(h2) : null,
            HouseholdRole = r.HouseholdRole,
            MemberId = r.MemberId is { } m && members.ContainsKey(m) ? m : null,
            MemberName = r.MemberId is { } m2 ? members.GetValueOrDefault(m2) : null,
            ApprovedAtUtc = r.ApprovedAtUtc,
            ApprovalTrigger = r.ApprovalTrigger,
            ContractId = r.ContractId,
            DuesSubscriptionId = r.DuesSubscriptionId,
            InitiationChargeId = r.InitiationChargeId,
            MoneyPending = r.MoneyPending,
            MarketingConsent = consent.GetValueOrDefault(r.Id, ConsentStates.Unknown),
            Version = r.Version,
        }).ToList();
    }

    /// <summary>Two names that are one person's: case, accents and extra spaces aside.</summary>
    internal static bool SameName(string? a, string? b) => string.Equals(Fold(a), Fold(b), StringComparison.Ordinal);

    private static string Fold(string? name)
    {
        var decomposed = string.Join(' ', (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)).Normalize(NormalizationForm.FormD);
        var kept = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) kept.Append(char.ToLowerInvariant(c));
        return kept.ToString().Normalize(NormalizationForm.FormC);
    }

    private static (string First, string Last) SplitName(string name)
    {
        var parts = (name ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => ("", ""),
            1 => (parts[0], ""),
            _ => (string.Join(' ', parts[..^1]), parts[^1]),
        };
    }

    /// <summary>A tier's cadence as a subscription's: Monthly unless it is one of the others.</summary>
    private static string Cadence(string? cadence) => cadence is "Quarterly" or "SemiAnnual" or "Annual" ? cadence : "Monthly";

    private static string Name(ClubMember member) => $"{member.FirstName} {member.LastName}".Trim();

    private static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Clip(string value, int max) => value.Length <= max ? value : value[..max];
}
