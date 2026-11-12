using System.Globalization;
using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ACI.Infrastructure.Services.Privacy;

public sealed partial class PersonDataService
{
    /// <summary>What an erasure writes where it took text out.</summary>
    private const string RemovedText = "(Removed when the person was erased.)";

    /// <summary>Why kept money rows keep their amounts: the books.</summary>
    private const string BooksNote = "Amounts, dates and VAT stay, under the pseudonym: Swiss law keeps the books for ten years (OR art. 958f).";

    public async Task<Result<ErasurePlanDto>> PlanErasureAsync(Guid organizationId, PersonSelection selection, CancellationToken ct = default)
    {
        var found = await SubjectAsync(organizationId, selection, track: false, ct);
        if (!found.IsSuccess) return found.Error;
        var d = await PersonDossier.LoadAsync(_db, organizationId, found.Value, track: false, ct);
        var now = DateTime.UtcNow;
        var lines = Lines(d, now, cancelledAhead: null);
        return Result.Success(new ErasurePlanDto(
            d.Subject.RecordCount, lines, Warnings(d, now),
            d.Subject.Addresses.OrderBy(a => a, StringComparer.Ordinal).ToList(),
            d.Subject.SharedAddresses.OrderBy(a => a, StringComparer.Ordinal).ToList()));
    }

    public async Task<Result<ErasureResultDto>> EraseAsync(Guid organizationId, Guid userId, EraseRequest request, CancellationToken ct = default)
    {
        if (!string.Equals(request.Confirm?.Trim(), "ERASE", StringComparison.Ordinal)) return DomainErrors.Privacy.ConfirmationMissing;
        var reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim();
        if (reference is { Length: > 200 }) return DomainErrors.Privacy.TooLong("The reference", 200);
        if (reference != null && reference.Contains('@')) return DomainErrors.Privacy.ReferenceHasPersonalData;
        var actor = await ActorNameAsync(userId, ct);
        return await EraseAsync(organizationId, request, PersonErasureTriggers.Request, userId, actor, reference, ct);
    }

    /// <summary>
    /// Erases a selection: by request (<see cref="EraseAsync(Guid, Guid, EraseRequest, CancellationToken)"/>),
    /// or a former member the retention settings have kept long enough.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bookings and places on event lists still to come are cancelled first, each through its
    /// own service, so a deposit or a ticket on the account is taken back the way staff taking
    /// it back would, the waitlist moves up and the money's audit lines are written. Those
    /// services run their own transactions, so this happens before the erasure's; if the
    /// erasure then fails, the bookings stay cancelled and running it again finishes the rest.
    /// </para>
    /// <para>
    /// Everything else is one save in one transaction: deletes, anonymised rows, the
    /// suppression list, the erasure's number and the single audit line, which names nobody.
    /// The automatic audit trail is off for that save, or every row it deletes would write a
    /// line with the person's name on it.
    /// </para>
    /// </remarks>
    internal async Task<Result<ErasureResultDto>> EraseAsync(
        Guid organizationId, PersonSelection selection, string trigger, Guid? userId, string? actor, string? reference, CancellationToken ct,
        string? retentionNote = null)
    {
        // The retention job removes stale records by the hundred: it leaves out the search of
        // everyone else's notes and mail for mentions, which reads whole tables each time.
        var mentions = trigger == PersonErasureTriggers.Request;
        var first = await SubjectAsync(organizationId, selection, track: false, ct);
        if (!first.IsSuccess) return first.Error;
        if (reference != null && first.Value.Names.Any(n => reference.Contains(n, StringComparison.OrdinalIgnoreCase)))
            return DomainErrors.Privacy.ReferenceHasPersonalData;
        var now = DateTime.UtcNow;
        var before = await PersonDossier.LoadAsync(_db, organizationId, first.Value, track: false, ct, mentions);
        // The retention settings remove people nobody has heard from in years. Someone with a
        // booking or an event place still to come is not one of them, whatever their lead says:
        // the run leaves them, and never cancels anything of theirs (its rules look for this too).
        if (trigger == PersonErasureTriggers.Retention && (UpcomingRegistrations(before, now).Any() || UpcomingReservations(before, now).Any()))
            return DomainErrors.Privacy.BookingsAhead;
        var ahead = await CancelAheadAsync(organizationId, before, now, ct);
        _db.ChangeTracker.Clear();

        for (var attempt = 1; ; attempt++)
        {
            Error? refused = null;
            ErasureResultDto? done = null;
            try
            {
                await RecordMerge.InTransactionAsync(_db, async () =>
                {
                    var subject = await SubjectAsync(organizationId, selection, track: true, ct);
                    if (!subject.IsSuccess)
                    {
                        refused = subject.Error;
                        return false;
                    }
                    var d = await PersonDossier.LoadAsync(_db, organizationId, subject.Value, track: true, ct, mentions);
                    var number = (await _db.PersonErasures.Where(e => e.OrganizationId == organizationId).MaxAsync(e => (int?)e.Number, ct) ?? 0) + 1;
                    var pseudonym = (d.Subject.Members.Count > 0 ? "Erased member " : "Erased person ") + number.ToString(CultureInfo.InvariantCulture);
                    var lines = Lines(d, now, ahead);
                    Apply(d, organizationId, pseudonym, now, suppress: trigger == PersonErasureTriggers.Request);

                    var summary = lines.GroupBy(l => l.Area).ToDictionary(g => g.Key, g => g.Sum(l => l.Count));
                    _db.PersonErasures.Add(new PersonErasure
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = organizationId,
                        Number = number,
                        Trigger = trigger,
                        ErasedAtUtc = now,
                        ErasedByUserId = userId,
                        ErasedByName = actor,
                        Reference = reference,
                        SummaryJson = JsonSerializer.Serialize(summary),
                    });
                    _db.AuditEvents.Add(new AuditEvent
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = organizationId,
                        OwnerUserId = userId ?? Guid.Empty,
                        EntityType = "privacy",
                        EntityId = null,
                        EntityLabel = pseudonym,
                        Action = "erased",
                        Summary = Clip(trigger == PersonErasureTriggers.Request
                            ? $"A person was erased at their request ({d.Subject.RecordCount} {Plural(d.Subject.RecordCount, "record")}, erasure {number}{(reference == null ? "" : $", reference {reference}")})"
                            : $"{retentionNote ?? "Removed by the retention settings"} (erasure {number})", 500),
                        ActorUserId = userId,
                        ActorName = Clip(actor ?? AuditVocabulary.SystemActor, 200),
                        CreatedAtUtc = now.AddTicks(1),
                    });

                    var auditing = _db.RecordsAuditTrail;
                    _db.RecordsAuditTrail = false;
                    try
                    {
                        await _db.SaveChangesAsync(ct);
                    }
                    finally
                    {
                        _db.RecordsAuditTrail = auditing;
                    }
                    done = new ErasureResultDto(number, pseudonym, now, lines);
                    return true;
                }, ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                _db.ChangeTracker.Clear();
                return DomainErrors.Privacy.ErasureConflict;
            }
            catch (DbUpdateException ex) when (attempt < 3 && IsUniqueViolation(ex))
            {
                // Two erasures took the same number at once; the other one keeps it.
                _db.ChangeTracker.Clear();
                continue;
            }
            if (refused != null) return refused;
            _logger.LogInformation("Erased person {Number} in {OrganizationId} ({Trigger})", done!.Number, organizationId, trigger);
            return Result.Success(done!);
        }
    }

    /// <summary>
    /// Cancels the person's bookings and event places still to come, through the services that
    /// own them. Returns how many of each it cancelled.
    /// </summary>
    private async Task<(int Registrations, int Reservations)> CancelAheadAsync(Guid organizationId, PersonDossier d, DateTime now, CancellationToken ct)
    {
        var registrations = 0;
        foreach (var r in UpcomingRegistrations(d, now))
        {
            var result = await _events.SetStatusAsync(organizationId, r.EventId, r.Id,
                new RegistrationStatusRequest { Status = RegistrationStatuses.Cancelled, Reason = "The person was erased at their request." }, ct);
            if (result.IsSuccess) registrations++;
            else _logger.LogWarning("An event place of an erased person was not cancelled: {Error}", result.Error.Description);
        }
        var reservations = 0;
        foreach (var r in UpcomingReservations(d, now))
        {
            var result = await _reservations.UpdateAsync(organizationId, r.Id, new ReservationWriteRequest { Status = ReservationStatuses.Cancelled }, expectedVersion: null, ct);
            if (result.IsSuccess) reservations++;
            else _logger.LogWarning("A booking of an erased person was not cancelled: {Error}", result.Error.Description);
        }
        return (registrations, reservations);
    }

    /// <summary>Their own places on event lists still to come; a guest they bring is someone else.</summary>
    private static IEnumerable<ClubEventRegistration> UpcomingRegistrations(PersonDossier d, DateTime now) =>
        d.Registrations.Where(r => r.Status is RegistrationStatuses.Reserved or RegistrationStatuses.Waitlisted
            && !(r.HostMemberId is Guid host && d.Subject.MemberIds.Contains(host) && !(r.MemberId is Guid m && d.Subject.MemberIds.Contains(m)))
            && d.Events.TryGetValue(r.EventId, out var e) && e.StartAtUtc > now
            && e.Status is not (ClubEventStatuses.Cancelled or ClubEventStatuses.Completed));

    private static IEnumerable<ClubReservation> UpcomingReservations(PersonDossier d, DateTime now) =>
        d.Reservations.Where(r => r.StartAtUtc > now && r.Status is ReservationStatuses.Pending or ReservationStatuses.Confirmed);

    /// <summary>What happens to each kind of record, as the plan and the result list it.</summary>
    private static List<ErasureLine> Lines(PersonDossier d, DateTime now, (int Registrations, int Reservations)? cancelledAhead)
    {
        var s = d.Subject;
        var lines = new List<ErasureLine>();
        void Add(string area, string action, int count, string? note = null)
        {
            if (count > 0) lines.Add(new ErasureLine(area, action, count, note));
        }

        Add("Leads", ErasureActions.Delete, s.Leads.Count, "With their status history, files and custom fields.");
        Add("Contacts", ErasureActions.Delete, s.Contacts.Count, "With their files, custom fields and relationships.");
        Add("Membership applications", ErasureActions.Delete, s.Applications.Count);
        Add("Club member records", ErasureActions.Anonymise, s.Members.Count(m => m.ErasedAtUtc == null),
            "Kept so the books add up: renamed to the pseudonym, with email, phone, notes and links removed, and the membership cancelled.");
        Add("Deals", ErasureActions.Anonymise, s.Deals.Count, "Kept for the pipeline's figures: renamed to the pseudonym, the description, files and contact removed.");
        Add("Activities and notes on their records", ErasureActions.Delete, d.Activities.Count);
        Add("Notes on other records that mention them", ErasureActions.Anonymise, d.ActivityMentions.Count, "The mention is replaced by the pseudonym.");
        Add("Tasks", ErasureActions.Delete, d.Tasks.Count, d.TaskComments.Count > 0 ? $"With {d.TaskComments.Count} {Plural(d.TaskComments.Count, "comment")}." : null);
        Add("Notifications about them", ErasureActions.Delete, d.Notifications.Count);
        Add("Sequence enrolments", ErasureActions.Delete, d.Enrollments.Count);
        Add("Automation runs", ErasureActions.Delete, d.AutomationFires.Count);
        Add("Generated copy", ErasureActions.Delete, d.CopyHistory.Count);
        Add("Relationships between contacts", ErasureActions.Delete, d.Relationships.Count);
        Add("Duplicate decisions", ErasureActions.Delete, d.DuplicateDecisions.Count);
        Add("Leads they referred", ErasureActions.Anonymise, d.LeadsReferredByThem.Count, "The leads stay; only the link to them as referrer goes.");
        Add("Contracts not sent", ErasureActions.Delete, d.Contracts.Count(c => c.Status == ContractStatuses.Draft));
        Add("Contracts out for signature", ErasureActions.Cancel, d.Contracts.Count(c => c.Status == ContractStatuses.Sent),
            "Voided: the signing link stops working, and the text and their details are removed.");
        Add("Contracts declined or voided", ErasureActions.Anonymise, d.Contracts.Count(c => c.Status is ContractStatuses.Declined or ContractStatuses.Voided),
            "The text and their details are removed.");
        Add("Signed contracts", ErasureActions.Keep, d.Contracts.Count(c => c.Status is ContractStatuses.SignedByClient or ContractStatuses.Countersigned),
            "A signed contract is a legal record and is kept as it is.");
        Add("Hand-overs to the member portal", ErasureActions.Anonymise, d.Onboardings.Count);
        Add("Emails sent to them", ErasureActions.Anonymise, d.Outbound.Count(m => m.Status != OutboundStatuses.Queued),
            "The text and attachments are removed and the recipient replaced by the pseudonym; when it went and whether it arrived stay.");
        Add("Emails waiting to go to them", ErasureActions.Cancel, d.Outbound.Count(m => m.Status == OutboundStatuses.Queued));
        Add("Emails to others that mention them", ErasureActions.Anonymise, d.OutboundMentions.Count, "The mention is replaced by the pseudonym.");
        Add("Emails from the older composer", ErasureActions.Anonymise, d.SentMessages.Count);
        Add("Replies from them", ErasureActions.Anonymise, d.Inbound.Count, "The sender and subject are removed.");
        Add("Club messages and campaign emails", ErasureActions.Anonymise, d.MemberMessages.Count, "The campaigns' totals stay.");
        Add("Consent records", ErasureActions.Delete, d.Consent.Count);
        Add("Club money records", ErasureActions.Anonymise,
            d.Charges.Count + d.Payments.Count + d.Tabs.Count + d.Sales.Count + d.PosTickets.Count + d.OnlinePayments.Count
            + d.BankTransactions.Count + d.Donations.Count, BooksNote);
        Add("Subscriptions", ErasureActions.Cancel, d.Subscriptions.Count(x => x.Status != "Cancelled"), "No more dues are billed. " + BooksNote);
        Add("Gift cards", ErasureActions.Anonymise, d.GiftCards.Count, "Their names and address are removed; the card's code and balance stay.");
        Add("Saved cards", ErasureActions.Delete, d.PaymentProfiles.Count, "Nothing more is charged to a card they saved.");
        Add("Member codes at the till", ErasureActions.Delete, d.MemberCodes.Count);
        Add("Household places", ErasureActions.Delete, d.HouseholdLinks.Count(l => l.MemberId is Guid m && s.MemberIds.Contains(m) || l.ContactId is Guid c && s.ContactIds.Contains(c)));
        var upcomingRegistrations = cancelledAhead?.Registrations ?? UpcomingRegistrations(d, now).Count();
        var upcomingReservations = cancelledAhead?.Reservations ?? UpcomingReservations(d, now).Count();
        Add("Event places still to come", ErasureActions.Cancel, upcomingRegistrations, "A ticket on their account is taken back, and the waitlist moves up.");
        Add("Bookings still to come", ErasureActions.Cancel, upcomingReservations, "A deposit on their account is taken back.");
        Add("Event places on the lists", ErasureActions.Anonymise, d.Registrations.Count - (cancelledAhead == null ? upcomingRegistrations : 0),
            "Kept under the pseudonym, so the events' figures stay.");
        Add("Bookings in the diary", ErasureActions.Anonymise, d.Reservations.Count - (cancelledAhead == null ? upcomingReservations : 0),
            "Kept under the pseudonym, with their notes removed.");
        Add("Visits", ErasureActions.Anonymise, d.Visits.Count, "Kept for the club's figures, under the pseudonym.");
        Add("Loyalty points", ErasureActions.Anonymise, d.Loyalty.Count, "Kept for the club's figures, under the pseudonym.");
        Add("Older club records kept read-only", ErasureActions.Anonymise, d.Documents.Count);
        Add("Lines of the audit trail", ErasureActions.Anonymise, d.Audit.Count, "Their name is replaced by the pseudonym; who did what and when stays.");
        Add("Addresses", ErasureActions.Delete, s.Addresses.Count,
            "Put on the suppression list as a fingerprint, so nothing is emailed to them and an import does not bring them back without anyone noticing.");
        return lines;
    }

    /// <summary>What staff should know before they confirm.</summary>
    private static List<string> Warnings(PersonDossier d, DateTime now)
    {
        var s = d.Subject;
        var warnings = new List<string>();
        foreach (var member in s.Members.Where(m => m.ErasedAtUtc == null && m.HouseAccountBalance != 0))
        {
            var currency = d.HouseAccounts.FirstOrDefault(a => a.MemberId == member.Id)?.Currency ?? "CHF";
            var amount = Math.Abs(member.HouseAccountBalance).ToString("N2", CultureInfo.InvariantCulture);
            warnings.Add(member.HouseAccountBalance < 0
                ? $"{member.FirstName} {member.LastName} owes {currency} {amount}. The debt stays in the books under the pseudonym, but nobody can bill or remind them any more."
                : $"{member.FirstName} {member.LastName} has {currency} {amount} in credit. Pay it back before erasing them, if it is theirs to have.");
        }
        if (d.PaymentProfiles.Any(p => p.PaymentMethodId != null || p.CustomerId != null))
            warnings.Add("A card is saved with Stripe. Cadence forgets it, but the customer stays in the club's Stripe account: delete it there too.");
        var signed = d.Contracts.Count(c => c.Status is ContractStatuses.SignedByClient or ContractStatuses.Countersigned);
        if (signed > 0)
            warnings.Add($"{signed} signed {Plural(signed, "contract")} {(signed == 1 ? "is" : "are")} kept as {(signed == 1 ? "it is" : "they are")}: a signed contract is a legal record.");
        if (d.Tabs.Any(t => t.Status == "Open"))
            warnings.Add("They have a tab still open. Close or void it first if they should not be charged for it.");
        foreach (var link in d.HouseholdLinks.Where(l => l.IsPrimary && l.MemberId is Guid m && s.MemberIds.Contains(m)))
            if (d.Households.TryGetValue(link.HouseholdId, out var household))
                warnings.Add($"They head the household {household.Name}; the next person to have joined it takes over.");
        if (s.SharedAddresses.Count > 0)
            warnings.Add($"{string.Join(", ", s.SharedAddresses.OrderBy(a => a))} {(s.SharedAddresses.Count == 1 ? "is" : "are")} also used by someone who is not being erased, so {(s.SharedAddresses.Count == 1 ? "it is" : "they are")} not put on the suppression list.");
        if (d.PosTickets.Count > 0)
            warnings.Add("The till (Lightspeed) may hold them as a customer too: remove them there.");
        if (UpcomingRegistrations(d, now).Any() || UpcomingReservations(d, now).Any())
            warnings.Add("Their event places and bookings still to come are cancelled first; a ticket or deposit on their account is taken back as credit.");
        return warnings;
    }

    /// <summary>
    /// Deletes, anonymises and cancels everything in the dossier, on tracked rows; the caller saves.
    /// </summary>
    private void Apply(PersonDossier d, Guid organizationId, string pseudonym, DateTime now, bool suppress)
    {
        var s = d.Subject;
        // What their text looks like, taken before the member rows below are renamed.
        s.FreezeNeedles();
        var memberIds = s.MemberIds;
        var contactIds = s.ContactIds;
        string? Scrub(string? text) => s.Scrub(text, pseudonym);

        // ── Sales ───────────────────────────────────────────────────────────
        _db.Activities.RemoveRange(d.Activities);
        foreach (var a in d.ActivityMentions)
        {
            a.Subject = Scrub(a.Subject);
            a.Body = Scrub(a.Body);
            a.Participants = Scrub(a.Participants);
        }
        _db.TaskComments.RemoveRange(d.TaskComments);
        _db.TaskItems.RemoveRange(d.Tasks);
        _db.LeadStatusEvents.RemoveRange(d.LeadStatusEvents);
        _db.OutreachEnrollments.RemoveRange(d.Enrollments);
        _db.AutomationFires.RemoveRange(d.AutomationFires);
        _db.ContactRelationships.RemoveRange(d.Relationships);
        _db.DuplicateDecisions.RemoveRange(d.DuplicateDecisions);
        _db.CopyHistoryItems.RemoveRange(d.CopyHistory);
        _db.Notifications.RemoveRange(d.Notifications);
        _db.CustomFieldValues.RemoveRange(d.CustomFieldValues);
        foreach (var lead in d.LeadsReferredByThem) lead.ReferredByContactId = null;
        // Records of theirs that staff kept out of the selection stay, but stop pointing at what
        // goes. A lead whose contact goes is marked so no new contact is made for it on its next
        // save (EnsurePersonContactAsync), which would bring the person back.
        foreach (var contact in d.ContactsFromTheirLeads) contact.ConvertedFromLeadId = null;
        foreach (var lead in d.LeadsOfTheirContacts)
        {
            lead.ConvertedToContactId = null;
            lead.PersonContactRemovedAtUtc ??= now;
        }

        foreach (var deal in s.Deals)
        {
            deal.Name = $"Deal of {pseudonym}";
            deal.Description = null;
            deal.ContactId = null;
            deal.UpdatedAtUtc = now;
        }
        // A lead's and a contact's files go with them (RecordDependents); a deal stays, so its files go here.
        foreach (var file in d.Attachments.Where(a => a.EntityType == RecordTypes.Deal))
            _db.Entry(new Attachment { Id = file.Id }).State = EntityState.Deleted;

        foreach (var contract in d.Contracts)
        {
            switch (contract.Status)
            {
                case ContractStatuses.Draft:
                    _db.ContractEvents.RemoveRange(d.ContractEvents.Where(e => e.ContractId == contract.Id));
                    _db.Contracts.Remove(contract);
                    break;
                case ContractStatuses.Sent or ContractStatuses.Declined or ContractStatuses.Voided:
                    if (contract.Status == ContractStatuses.Sent)
                    {
                        contract.Status = ContractStatuses.Voided;
                        contract.ClosedReason = "Voided when the person was erased at their request.";
                    }
                    contract.SigningTokenHash = null;
                    contract.SigningTokenExpiresAtUtc = null;
                    contract.Title = Scrub(contract.Title) ?? contract.Title;
                    contract.Body = RemovedText;
                    contract.CounterpartyName = pseudonym;
                    contract.CounterpartyEmail = "";
                    contract.ClientSignatureName = null;
                    contract.ClientSignatureIp = null;
                    contract.ClientSignatureUserAgent = null;
                    contract.UpdatedAtUtc = now;
                    foreach (var e in d.ContractEvents.Where(e => e.ContractId == contract.Id))
                    {
                        e.Ip = null;
                        e.Detail = Scrub(e.Detail);
                        e.ActorLabel = Scrub(e.ActorLabel);
                    }
                    break;
                // Signed by the person: a legal record, kept whole.
            }
        }
        foreach (var o in d.Onboardings)
        {
            o.Name = pseudonym;
            o.Email = "";
            o.Phone = null;
            o.WelcomeMessage = null;
            o.EmailSubject = Scrub(o.EmailSubject);
            o.EmailHtml = null;
            o.UpdatedAtUtc = now;
        }

        // ── Mail ────────────────────────────────────────────────────────────
        foreach (var m in d.Outbound)
        {
            var othersLeft = s.WithoutPerson(m.To) != null || s.WithoutPerson(m.Cc) != null || s.WithoutPerson(m.Bcc) != null;
            var theirs = m.LeadId is Guid l && s.LeadIds.Contains(l) || m.ContactId is Guid c && contactIds.Contains(c)
                || m.MemberId is Guid mm && memberIds.Contains(mm) || !othersLeft;
            if (m.Status == OutboundStatuses.Queued && theirs)
            {
                m.Status = OutboundStatuses.Cancelled;
                m.NextAttemptAtUtc = null;
                m.LastError = "Cancelled: the person was erased before it went.";
            }
            m.To = othersLeft ? s.WithoutPerson(m.To) : pseudonym;
            m.Cc = s.WithoutPerson(m.Cc);
            m.Bcc = s.WithoutPerson(m.Bcc);
            m.RecipientName = theirs ? pseudonym : Scrub(m.RecipientName);
            m.Subject = Scrub(m.Subject);
            m.UnsubscribeUrl = null;
            m.ReplyToken = null;
            m.PayloadJson = m.Channel == OutboundChannels.Webhook ? null : m.PayloadJson;
            if (theirs)
            {
                // A message to the person: its words go. Sent to others as well, they keep theirs,
                // without the person in them.
                m.Body = m.Body == null ? null : RemovedText;
                m.TextBody = null;
                m.HtmlBody = null;
            }
            else
            {
                m.Body = Scrub(m.Body);
                m.TextBody = Scrub(m.TextBody);
                m.HtmlBody = Scrub(m.HtmlBody);
            }
        }
        foreach (var id in d.OutboundAttachmentIds)
            _db.Entry(new OutboundAttachment { Id = id }).State = EntityState.Deleted;
        foreach (var m in d.OutboundMentions)
        {
            m.Subject = Scrub(m.Subject);
            m.Body = Scrub(m.Body);
            m.TextBody = Scrub(m.TextBody);
            m.HtmlBody = Scrub(m.HtmlBody);
            m.PayloadJson = Scrub(m.PayloadJson);
            m.RecipientName = Scrub(m.RecipientName);
        }
        foreach (var m in d.SentMessages)
        {
            var others = s.WithoutPerson(m.To);
            m.To = others ?? pseudonym;
            m.Cc = s.WithoutPerson(m.Cc);
            m.Bcc = s.WithoutPerson(m.Bcc);
            m.Subject = Scrub(m.Subject) ?? "";
            m.Body = others == null ? RemovedText : Scrub(m.Body) ?? "";
        }
        foreach (var i in d.Inbound)
        {
            i.FromAddress = pseudonym;
            i.Subject = null;
        }
        foreach (var m in d.MemberMessages)
        {
            m.MemberName = pseudonym;
            m.Email = null;
            m.Subject = Scrub(m.Subject) ?? "";
            m.Preview = null;
        }
        _db.ConsentRecords.RemoveRange(d.Consent);

        // The suppression list never keeps their address itself, only a fingerprint of it
        // (ErasedAddresses), which still stops mail if the address comes back.
        // On request: what the mail service reported goes, and a fingerprint marked "erased"
        // takes its place, which also stops an import or a new member with the address.
        // By the retention settings nobody asked to be forgotten, but an unsubscribe, a complaint
        // or a bounce they gave must still hold after their lead goes: each such row keeps its
        // reason, under the fingerprint instead of the address.
        var held = d.Suppressions.Where(x => ErasedAddresses.IsKey(x.Email)).ToDictionary(x => x.Email, StringComparer.Ordinal);
        foreach (var row in d.Suppressions.Where(x => !ErasedAddresses.IsKey(x.Email)))
        {
            var key = ErasedAddresses.Key(row.Email);
            if (suppress || held.ContainsKey(key))
            {
                _db.EmailSuppressions.Remove(row);
                continue;
            }
            row.Email = key;
            row.Detail = Scrub(row.Detail);
            held[key] = row;
        }
        if (suppress)
        {
            foreach (var address in s.Addresses)
            {
                var key = ErasedAddresses.Key(address);
                if (held.TryGetValue(key, out var existing))
                {
                    // An unsubscribe kept from an earlier retention run becomes an erasure, which
                    // also stops an import or a new member with the address.
                    if (existing.Reason == EmailSuppressionReasons.Erased) continue;
                    existing.Reason = EmailSuppressionReasons.Erased;
                    existing.Detail = $"Erased at their request ({pseudonym}).";
                    existing.CreatedAtUtc = now;
                    continue;
                }
                _db.EmailSuppressions.Add(new EmailSuppression
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    Email = key,
                    Reason = EmailSuppressionReasons.Erased,
                    Detail = $"Erased at their request ({pseudonym}).",
                    CreatedAtUtc = now,
                });
            }
        }

        // ── Club ────────────────────────────────────────────────────────────
        foreach (var member in s.Members)
        {
            member.FirstName = "Erased";
            member.LastName = pseudonym.StartsWith("Erased ", StringComparison.Ordinal) ? pseudonym["Erased ".Length..] : pseudonym;
            member.Email = "";
            member.Phone = null;
            member.Notes = null;
            member.ContactId = null;
            member.LeadId = null;
            member.DealId = null;
            if (member.Status is "Active" or "Pending")
            {
                member.Status = "Cancelled";
                member.BillingLapsedAtUtc = null;
                member.StatusBeforeLapse = null;
            }
            member.ErasedAtUtc ??= now;
        }
        _db.ClubApplications.RemoveRange(s.Applications);
        foreach (var v in d.Visits)
        {
            v.MemberName = pseudonym;
            v.Notes = null;
        }
        foreach (var e in d.Loyalty)
        {
            e.MemberName = pseudonym;
            e.Note = null;
        }
        foreach (var c in d.Charges)
        {
            if (memberIds.Contains(c.MemberId)) c.MemberName = pseudonym;
            if (c.IncurredByMemberId is Guid by && memberIds.Contains(by)) c.IncurredByName = pseudonym;
            c.Description = Scrub(c.Description) ?? "";
            c.Reason = Scrub(c.Reason);
            c.Reference = Scrub(c.Reference);
        }
        foreach (var p in d.Payments)
        {
            p.MemberName = pseudonym;
            p.Description = Scrub(p.Description);
            p.Reference = Scrub(p.Reference);
            p.Reason = Scrub(p.Reason);
            p.Notes = null;
        }
        foreach (var x in d.Subscriptions)
        {
            x.MemberName = pseudonym;
            x.Notes = null;
            if (x.Status != "Cancelled")
            {
                x.Status = "Cancelled";
                x.NextChargeAtUtc = null;
                x.UpdatedAtUtc = now;
            }
        }
        foreach (var t in d.Tabs)
        {
            t.MemberName = pseudonym;
            t.Notes = null;
        }
        foreach (var x in d.Donations)
        {
            x.MemberName = pseudonym;
            x.Message = null;
        }
        foreach (var g in d.GiftCards)
        {
            if (g.PurchaserMemberId is Guid buyer && memberIds.Contains(buyer)) g.PurchaserName = pseudonym;
            else g.PurchaserName = Scrub(g.PurchaserName) ?? "";
            if (g.RecipientEmail != null && s.Addresses.Contains(ErasedAddresses.Normalize(g.RecipientEmail)))
            {
                g.RecipientName = pseudonym;
                g.RecipientEmail = null;
            }
            else g.RecipientName = Scrub(g.RecipientName) ?? "";
            g.Message = null;
        }
        foreach (var t in d.GiftCardTransactions.Where(t => t.MemberId is Guid m && memberIds.Contains(m)))
            t.RedeemedByName = pseudonym;
        foreach (var x in d.Sales)
        {
            if (memberIds.Contains(x.MemberId)) x.MemberName = pseudonym;
            x.Notes = null;
        }
        foreach (var t in d.PosTickets)
        {
            t.MemberName = pseudonym;
            t.CustomerName = pseudonym;
            t.CustomerReference = null;
            t.CustomerId = null;
            t.DataJson = null;
        }
        foreach (var p in d.OnlinePayments)
        {
            p.MemberName = pseudonym;
            p.Description = Scrub(p.Description) ?? "";
        }
        foreach (var t in d.BankTransactions)
        {
            t.MemberName = pseudonym;
            t.DebtorName = pseudonym;
            t.DebtorIban = null;
            t.Message = Scrub(t.Message);
        }
        _db.ClubPaymentProfiles.RemoveRange(d.PaymentProfiles);
        _db.ClubMemberCodes.RemoveRange(d.MemberCodes);
        foreach (var r in d.Registrations)
        {
            if (r.MemberId is Guid m && memberIds.Contains(m) || r.Email != null && s.Addresses.Contains(ErasedAddresses.Normalize(r.Email)))
            {
                r.Name = pseudonym;
                r.Email = null;
            }
            if (r.HostMemberId is Guid host && memberIds.Contains(host)) r.HostName = pseudonym;
            r.CancelReason = Scrub(r.CancelReason);
        }
        foreach (var r in d.Reservations)
        {
            r.MemberName = pseudonym;
            r.MemberEmail = null;
            r.Notes = null;
        }
        LeaveHouseholds(d, now);
        // Read as JSON: an old application keeps the names apart from each other, with the phone
        // beside them, and the default escaping hides an accented name from a plain replacement.
        foreach (var doc in d.Documents)
        {
            doc.Payload = ClubDocumentScrub.Scrub(doc.Payload, s, pseudonym);
            doc.UpdatedAtUtc = now;
        }

        // ── The audit trail: their records keep their history, under the pseudonym ──
        // A task about them goes, and its title may say their first name alone; a deal is renamed.
        var personRecords = s.RecordIds;
        var taskIds = d.Tasks.Select(t => t.Id).ToHashSet();
        var dealIds = s.DealIds;
        foreach (var line in d.Audit)
        {
            line.EntityLabel = line.EntityId is not Guid id ? Scrub(line.EntityLabel)
                : personRecords.Contains(id) ? pseudonym
                : taskIds.Contains(id) ? $"A task about {pseudonym}"
                : dealIds.Contains(id) ? $"Deal of {pseudonym}"
                : Scrub(line.EntityLabel);
            line.Summary = Scrub(line.Summary) ?? "";
        }

        // Last, so everything above that pointed at them was let go first.
        _db.Leads.RemoveRange(s.Leads);
        _db.Contacts.RemoveRange(s.Contacts);
    }

    /// <summary>
    /// Takes the members out of their households. One who headed a household with others in it
    /// hands it to the next person to have joined, as a member merged away or deleted does; a
    /// household nobody is left in goes. A contact's place is left to the contact's own delete
    /// (RecordDependents), which does the same.
    /// </summary>
    private void LeaveHouseholds(PersonDossier d, DateTime now)
    {
        var memberIds = d.Subject.MemberIds;
        foreach (var link in d.HouseholdLinks.Where(l => l.MemberId is Guid m && memberIds.Contains(m)))
        {
            _db.ClubHouseholdMembers.Remove(link);
            if (!d.Households.TryGetValue(link.HouseholdId, out var household)) continue;
            var left = _db.ClubHouseholdMembers.Local.Where(l => l.HouseholdId == link.HouseholdId && _db.Entry(l).State != EntityState.Deleted)
                .Concat(_db.ClubHouseholdMembers.Where(l => l.HouseholdId == link.HouseholdId).ToList())
                .Where(l => _db.Entry(l).State != EntityState.Deleted)
                .DistinctBy(l => l.Id)
                .OrderBy(l => l.CreatedAtUtc).ThenBy(l => l.Id)
                .ToList();
            if (left.Count == 0)
            {
                _db.ClubHouseholds.Remove(household);
                continue;
            }
            if (link.IsPrimary)
            {
                var next = left[0];
                next.IsPrimary = true;
                next.Role = HouseholdRoles.Primary;
                next.RollUpBilling = false;
                next.UpdatedAtUtc = now;
                if (next.MemberId == null)
                    foreach (var other in left.Skip(1).Where(l => l.RollUpBilling))
                    {
                        other.RollUpBilling = false;
                        other.UpdatedAtUtc = now;
                    }
            }
            household.UpdatedAtUtc = now;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true;
}
