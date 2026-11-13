using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ACI.Infrastructure.Services.Privacy;

/// <summary>
/// Finds, exports and erases one person across everything a workspace holds (see
/// <see cref="IPersonData"/>).
/// </summary>
/// <remarks>
/// Every read and write is the workspace's own: the query filters keep another workspace's
/// rows out, and a record id from another workspace is simply not found. Where the person's
/// data lives is listed in <see cref="PersonDataInventory"/>; a test fails when a table is
/// added that the inventory does not place.
/// </remarks>
public sealed partial class PersonDataService : IPersonData
{
    /// <summary>More records than one person has: a lookup stops widening here.</summary>
    private const int MaxRecords = 100;

    private readonly AppDbContext _db;
    private readonly IClubEvents _events;
    private readonly IClubReservations _reservations;
    private readonly ILogger<PersonDataService> _logger;

    public PersonDataService(AppDbContext db, IClubEvents events, IClubReservations reservations, ILogger<PersonDataService> logger)
    {
        _db = db;
        _events = events;
        _reservations = reservations;
        _logger = logger;
    }

    public async Task<Result<PersonFoundDto>> FindAsync(Guid organizationId, PersonLookup lookup, CancellationToken ct = default)
    {
        var addresses = new HashSet<string>(StringComparer.Ordinal);
        var found = new Dictionary<(string Kind, Guid Id), PersonRecordDto>();
        var leads = new Dictionary<Guid, Lead>();
        var contacts = new Dictionary<Guid, Contact>();
        var members = new Dictionary<Guid, ClubMember>();
        var applications = new Dictionary<Guid, ClubApplication>();

        void AddLead(Lead l, string why) { if (leads.TryAdd(l.Id, l)) found[(PersonRecordKinds.Lead, l.Id)] = new(PersonRecordKinds.Lead, l.Id, l.Name, LeadKindRules.PersonOf(l).Email, l.IsConverted ? "Converted" : l.Status, why); }
        void AddContact(Contact c, string why) { if (contacts.TryAdd(c.Id, c)) found[(PersonRecordKinds.Contact, c.Id)] = new(PersonRecordKinds.Contact, c.Id, c.Name, NullIfEmpty(c.Email), c.IsArchived ? "Archived" : null, why); }
        void AddMember(ClubMember m, string why) { if (members.TryAdd(m.Id, m)) found[(PersonRecordKinds.Member, m.Id)] = new(PersonRecordKinds.Member, m.Id, $"{m.FirstName} {m.LastName}".Trim(), NullIfEmpty(m.Email), m.ErasedAtUtc != null ? "Erased" : $"{m.Tier}, {m.Status}", why); }
        void AddApplication(ClubApplication a, string why) { if (applications.TryAdd(a.Id, a)) found[(PersonRecordKinds.Application, a.Id)] = new(PersonRecordKinds.Application, a.Id, $"{a.FirstName} {a.LastName}".Trim(), NullIfEmpty(a.Email), a.Status, why); }

        var email = ErasedAddresses.Normalize(lookup.Email);
        if (email.Length > 0)
        {
            if (!LooksLikeEmail(email)) return DomainErrors.Privacy.EmailInvalid;
            addresses.Add(email);
        }
        const string start = "The record you started from";
        if (lookup.LeadId is Guid leadId)
        {
            if (await _db.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.Id == leadId && l.OrganizationId == organizationId, ct) is not { } lead)
                return DomainErrors.Privacy.RecordNotFound;
            AddLead(lead, lead.Kind == LeadKinds.Establishment
                ? $"{start}, an establishment. Untick it to keep the place and take only its contact person off it"
                : start);
        }
        if (lookup.ContactId is Guid contactId)
        {
            if (await _db.Contacts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == contactId && c.OrganizationId == organizationId, ct) is not { } contact)
                return DomainErrors.Privacy.RecordNotFound;
            AddContact(contact, start);
        }
        if (lookup.MemberId is Guid memberId)
        {
            if (await _db.ClubMembers.AsNoTracking().FirstOrDefaultAsync(m => m.Id == memberId && m.OrganizationId == organizationId, ct) is not { } member)
                return DomainErrors.Privacy.RecordNotFound;
            AddMember(member, start);
        }
        if (addresses.Count == 0 && found.Count == 0) return DomainErrors.Privacy.NothingAsked;

        // Widens a few rounds: records with an address already found, and records linked to
        // ones already found, until nothing new turns up (or more than one person has).
        for (var round = 0; round < 4 && found.Count < MaxRecords; round++)
        {
            var before = found.Count;
            // A lead's address is its person's: an establishment's contact person's. The place's
            // own general address is the business's, and would gather whoever else writes from it.
            foreach (var r in leads.Values.Select(l => LeadKindRules.PersonOf(l).Email).Concat(contacts.Values.Select(c => c.Email))
                         .Concat(members.Values.Select(m => m.Email)).Concat(applications.Values.Select(a => a.Email)))
            {
                var a = ErasedAddresses.Normalize(r);
                if (a.Length > 0) addresses.Add(a);
            }
            var list = addresses.ToList();
            const string sameAddress = "Same email address";
            // Not an establishment: its address is the business's, and the person it names is its
            // contact person, whom an erasure takes off it while the place stays.
            foreach (var l in await _db.Leads.AsNoTracking()
                         .Where(l => l.OrganizationId == organizationId && l.Kind != LeadKinds.Establishment && list.Contains(l.Email.Trim().ToLower()))
                         .Take(MaxRecords).ToListAsync(ct))
                AddLead(l, sameAddress);
            foreach (var c in await _db.Contacts.AsNoTracking().Where(c => c.OrganizationId == organizationId && list.Contains(c.Email.Trim().ToLower())).Take(MaxRecords).ToListAsync(ct))
                AddContact(c, sameAddress);
            foreach (var m in await _db.ClubMembers.AsNoTracking().Where(m => m.OrganizationId == organizationId && list.Contains(m.Email.Trim().ToLower())).Take(MaxRecords).ToListAsync(ct))
                AddMember(m, sameAddress);
            foreach (var a in await _db.ClubApplications.AsNoTracking().Where(a => a.OrganizationId == organizationId && list.Contains(a.Email.Trim().ToLower())).Take(MaxRecords).ToListAsync(ct))
                AddApplication(a, sameAddress);

            // Links between the records: a lead's contact, a member's lead and contact, an application's.
            var leadIds = leads.Keys.ToList();
            var contactIds = contacts.Keys.ToList();
            var memberIds = members.Keys.ToList();
            var linkedContactIds = leads.Values.Where(l => l.ConvertedToContactId != null).Select(l => l.ConvertedToContactId!.Value)
                .Concat(members.Values.Where(m => m.ContactId != null).Select(m => m.ContactId!.Value))
                .Concat(applications.Values.Where(a => a.ContactId != null).Select(a => a.ContactId!.Value)).Distinct().ToList();
            foreach (var c in await _db.Contacts.AsNoTracking()
                         .Where(c => c.OrganizationId == organizationId && (linkedContactIds.Contains(c.Id) || (c.ConvertedFromLeadId != null && leadIds.Contains(c.ConvertedFromLeadId.Value))))
                         .Take(MaxRecords).ToListAsync(ct))
                AddContact(c, LinkedTo(c.ConvertedFromLeadId is Guid from && leads.TryGetValue(from, out var l) ? $"the lead {l.Name}" : "another of their records"));
            var linkedLeadIds = contacts.Values.Where(c => c.ConvertedFromLeadId != null).Select(c => c.ConvertedFromLeadId!.Value)
                .Concat(members.Values.Where(m => m.LeadId != null).Select(m => m.LeadId!.Value))
                .Concat(applications.Values.Where(a => a.LeadId != null).Select(a => a.LeadId!.Value)).Distinct().ToList();
            // An establishment linked to their contact is a business they speak for, not a record
            // of theirs: ticked here, it would be erased with all its history. Left out, an
            // erasure takes only its contact person off it (PersonDossier.EstablishmentsTheySpeakFor).
            foreach (var l in await _db.Leads.AsNoTracking()
                         .Where(l => l.OrganizationId == organizationId && l.Kind != LeadKinds.Establishment
                             && (linkedLeadIds.Contains(l.Id) || (l.ConvertedToContactId != null && contactIds.Contains(l.ConvertedToContactId.Value))))
                         .Take(MaxRecords).ToListAsync(ct))
                AddLead(l, LinkedTo(l.ConvertedToContactId is Guid to && contacts.TryGetValue(to, out var c) ? $"the contact {c.Name}" : "another of their records"));
            var linkedMemberIds = applications.Values.Where(a => a.MemberId != null).Select(a => a.MemberId!.Value).ToList();
            foreach (var m in await _db.ClubMembers.AsNoTracking()
                         .Where(m => m.OrganizationId == organizationId && (linkedMemberIds.Contains(m.Id)
                             || (m.ContactId != null && contactIds.Contains(m.ContactId.Value)) || (m.LeadId != null && leadIds.Contains(m.LeadId.Value))))
                         .Take(MaxRecords).ToListAsync(ct))
                AddMember(m, LinkedTo(m.ContactId is Guid mc && contacts.TryGetValue(mc, out var c) ? $"the contact {c.Name}" : "another of their records"));
            foreach (var a in await _db.ClubApplications.AsNoTracking()
                         .Where(a => a.OrganizationId == organizationId && ((a.MemberId != null && memberIds.Contains(a.MemberId.Value))
                             || (a.LeadId != null && leadIds.Contains(a.LeadId.Value)) || (a.ContactId != null && contactIds.Contains(a.ContactId.Value))))
                         .Take(MaxRecords).ToListAsync(ct))
                AddApplication(a, LinkedTo("another of their records"));

            if (found.Count == before) break;
        }

        static int Order(string kind) => kind switch
        {
            PersonRecordKinds.Member => 0, PersonRecordKinds.Contact => 1, PersonRecordKinds.Lead => 2, _ => 3,
        };
        var records = found.Values.OrderBy(r => Order(r.Kind)).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase).Take(MaxRecords).ToList();
        return Result.Success(new PersonFoundDto(addresses.OrderBy(a => a, StringComparer.Ordinal).ToList(), records));
    }

    public async Task<IReadOnlyList<PersonErasureDto>> ErasuresAsync(Guid organizationId, CancellationToken ct = default)
    {
        var rows = await _db.PersonErasures.AsNoTracking()
            .Where(e => e.OrganizationId == organizationId)
            .OrderByDescending(e => e.ErasedAtUtc)
            .Take(200)
            .ToListAsync(ct);
        return rows.Select(e => new PersonErasureDto(e.Number, e.Trigger, e.ErasedAtUtc, e.ErasedByName, e.Reference, ReadSummary(e.SummaryJson))).ToList();
    }

    /// <summary>
    /// The selection, read and checked: every record must be in the workspace. Tracked when
    /// it is read for an erasure.
    /// </summary>
    private async Task<Result<PersonSubject>> SubjectAsync(Guid organizationId, PersonSelection selection, bool track, CancellationToken ct)
    {
        var leadIds = selection.LeadIds.Distinct().ToList();
        var contactIds = selection.ContactIds.Distinct().ToList();
        var memberIds = selection.MemberIds.Distinct().ToList();
        var applicationIds = selection.ApplicationIds.Distinct().ToList();
        var asked = selection.Addresses.Select(ErasedAddresses.Normalize).Where(a => a.Length > 0).Distinct().ToList();
        if (leadIds.Count + contactIds.Count + memberIds.Count + applicationIds.Count + asked.Count == 0)
            return DomainErrors.Privacy.NothingChosen;
        if (leadIds.Count + contactIds.Count + memberIds.Count + applicationIds.Count > MaxRecords || asked.Count > 20)
            return DomainErrors.Privacy.TooManyRecords;
        if (asked.Any(a => !LooksLikeEmail(a))) return DomainErrors.Privacy.EmailInvalid;

        IQueryable<T> Q<T>(IQueryable<T> query) where T : class => track ? query : query.AsNoTracking();
        var leads = await Q(_db.Leads).Where(l => l.OrganizationId == organizationId && leadIds.Contains(l.Id)).ToListAsync(ct);
        var contacts = await Q(_db.Contacts).Where(c => c.OrganizationId == organizationId && contactIds.Contains(c.Id)).ToListAsync(ct);
        var members = await Q(_db.ClubMembers).Where(m => m.OrganizationId == organizationId && memberIds.Contains(m.Id)).ToListAsync(ct);
        var applications = await Q(_db.ClubApplications).Where(a => a.OrganizationId == organizationId && applicationIds.Contains(a.Id)).ToListAsync(ct);
        if (leads.Count != leadIds.Count || contacts.Count != contactIds.Count || members.Count != memberIds.Count || applications.Count != applicationIds.Count)
            return DomainErrors.Privacy.RecordNotFound;

        // A lead's address is its person's: an establishment's contact person's, not the place's own.
        var all = asked
            .Concat(leads.Select(l => LeadKindRules.PersonOf(l).Email)).Concat(contacts.Select(c => c.Email))
            .Concat(members.Select(m => m.Email)).Concat(applications.Select(a => a.Email))
            .Select(ErasedAddresses.Normalize).Where(a => a.Length > 0).ToHashSet(StringComparer.Ordinal);

        // An address a record outside the selection still uses is that other person's too.
        var list = all.ToList();
        var shared = new HashSet<string>(StringComparer.Ordinal);
        shared.UnionWith(await _db.Leads.AsNoTracking().Where(l => l.OrganizationId == organizationId && !leadIds.Contains(l.Id) && list.Contains(l.Email.Trim().ToLower())).Select(l => l.Email.Trim().ToLower()).ToListAsync(ct));
        shared.UnionWith(await _db.Contacts.AsNoTracking().Where(c => c.OrganizationId == organizationId && !contactIds.Contains(c.Id) && list.Contains(c.Email.Trim().ToLower())).Select(c => c.Email.Trim().ToLower()).ToListAsync(ct));
        shared.UnionWith(await _db.ClubMembers.AsNoTracking().Where(m => m.OrganizationId == organizationId && !memberIds.Contains(m.Id) && list.Contains(m.Email.Trim().ToLower())).Select(m => m.Email.Trim().ToLower()).ToListAsync(ct));
        shared.UnionWith(await _db.ClubApplications.AsNoTracking().Where(a => a.OrganizationId == organizationId && !applicationIds.Contains(a.Id) && list.Contains(a.Email.Trim().ToLower())).Select(a => a.Email.Trim().ToLower()).ToListAsync(ct));

        var subject = new PersonSubject
        {
            Leads = leads,
            Contacts = contacts,
            Members = members,
            Applications = applications,
            Addresses = all.Where(a => !shared.Contains(a)).ToHashSet(StringComparer.Ordinal),
            SharedAddresses = shared,
        };
        // The deals made for them: on their contact, or the one their lead was converted into.
        var dealIds = leads.Where(l => l.ConvertedToDealId != null).Select(l => l.ConvertedToDealId!.Value).ToList();
        var contactIdList = subject.ContactIds.ToList();
        subject.Deals = await Q(_db.Deals)
            .Where(d => d.OrganizationId == organizationId && ((d.ContactId != null && contactIdList.Contains(d.ContactId.Value)) || dealIds.Contains(d.Id)))
            .ToListAsync(ct);
        return Result.Success(subject);
    }

    private static string LinkedTo(string what) => $"Linked to {what}";

    private static string? NullIfEmpty(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static bool LooksLikeEmail(string address) =>
        address.Length is > 2 and <= 256 && address.IndexOf('@') is > 0 and var at && at < address.Length - 1 && !address.Contains(' ');

    private static IReadOnlyDictionary<string, int> ReadSummary(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new Dictionary<string, int>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, int>();
        }
    }

    /// <summary>The name of the staff member acting, for the audit trail and the log.</summary>
    private async Task<string> ActorNameAsync(Guid userId, CancellationToken ct) =>
        await _db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Name).FirstOrDefaultAsync(ct) ?? "Someone";
}
