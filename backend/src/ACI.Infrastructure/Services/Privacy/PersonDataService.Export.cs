using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ACI.Application.Common;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services.Privacy;

public sealed partial class PersonDataService
{
    private static readonly JsonSerializerOptions ExportJson = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>One file of an export: where it goes in the zip, what it holds, and its rows.</summary>
    private sealed record ExportSection(string Path, string About, IReadOnlyList<object> Rows);

    public async Task<Result<PersonExportFile>> ExportAsync(Guid organizationId, Guid userId, PersonSelection selection, CancellationToken ct = default)
    {
        var found = await SubjectAsync(organizationId, selection, track: false, ct);
        if (!found.IsSuccess) return found.Error;
        var subject = found.Value;
        var d = await PersonDossier.LoadAsync(_db, organizationId, subject, track: false, ct);
        var workspace = await _db.Organizations.AsNoTracking().Where(o => o.Id == organizationId).Select(o => o.Name).FirstOrDefaultAsync(ct) ?? "This workspace";
        var actor = await ActorNameAsync(userId, ct);
        var now = DateTime.UtcNow;
        var sections = Sections(d);

        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var section in sections.Where(s => s.Rows.Count > 0))
            {
                var entry = zip.CreateEntry(section.Path, CompressionLevel.Optimal);
                await using var stream = entry.Open();
                await JsonSerializer.SerializeAsync(stream, new { about = section.About, count = section.Rows.Count, rows = section.Rows }, ExportJson, ct);
            }
            // The files on their records, read one at a time rather than all at once.
            foreach (var attachment in d.Attachments)
            {
                var content = await _db.Attachments.AsNoTracking().Where(a => a.Id == attachment.Id).Select(a => a.Content).FirstOrDefaultAsync(ct);
                if (content == null) continue;
                var entry = zip.CreateEntry($"files/{attachment.EntityType}-{attachment.RecordId:N}/{SafeFileName(attachment.FileName)}", CompressionLevel.Optimal);
                await using var stream = entry.Open();
                await stream.WriteAsync(content, ct);
            }
            var summary = zip.CreateEntry("summary.txt", CompressionLevel.Optimal);
            await using (var writer = new StreamWriter(summary.Open(), new UTF8Encoding(false)))
                await writer.WriteAsync(Summary(workspace, actor, now, d, sections));
        }

        // The audit trail says who downloaded whose data. It names the person through their
        // record, so erasing them later rewrites this line too.
        var (entityType, entityId, label) = Primary(subject);
        _db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            OwnerUserId = userId,
            EntityType = "privacy",
            EntityId = entityId,
            EntityLabel = label,
            Action = "exported",
            Summary = Clip($"Everything held about this person downloaded ({Items(sections, d)} items from {subject.RecordCount} {Plural(subject.RecordCount, "record")}{(entityType == null ? "" : $", starting from their {entityType}")})", 500),
            ActorUserId = userId,
            ActorName = Clip(actor, 200),
            CreatedAtUtc = now,
        });
        await _db.SaveChangesAsync(ct);

        var fileName = $"personal-data-{now:yyyy-MM-dd-HHmm}.zip";
        return Result.Success(new PersonExportFile(buffer.ToArray(), fileName));
    }

    /// <summary>Every file of an export, in the order the summary lists them.</summary>
    private List<ExportSection> Sections(PersonDossier d)
    {
        var s = d.Subject;
        var tasksById = d.TaskComments.GroupBy(c => c.TaskItemId).ToDictionary(g => g.Key, g => g.OrderBy(c => c.CreatedAtUtc).ToList());
        var itemsByTab = d.TabItems.GroupBy(i => i.TabId).ToDictionary(g => g.Key, g => g.ToList());
        var linesBySale = d.SaleLines.GroupBy(l => l.SaleId).ToDictionary(g => g.Key, g => g.OrderBy(l => l.SortOrder).ToList());
        var transactionsByCard = d.GiftCardTransactions.GroupBy(t => t.GiftCardId).ToDictionary(g => g.Key, g => g.OrderBy(t => t.AtUtc).ToList());
        var eventsByContract = d.ContractEvents.GroupBy(e => e.ContractId).ToDictionary(g => g.Key, g => g.ToList());
        var stepsByRun = d.AutomationRunSteps.GroupBy(x => x.RunId).ToDictionary(g => g.Key, g => g.OrderBy(x => x.AtUtc).ToList());

        return
        [
            new("records/members.json", "Their club member records.", s.Members.Select(m => Row(m)).ToList()),
            new("records/contacts.json", "Their contact records.", s.Contacts.Select(c => Row(c, nameof(Contact.PhoneKey), nameof(Contact.NameKey))).ToList()),
            new("records/leads.json", "Their lead records, with the progress through the sales pipeline.", s.Leads.Select(l => Row(l)).ToList()),
            new("records/applications.json", "Their membership applications.", s.Applications.Select(a => Row(a)).ToList()),
            new("records/deals.json", "Deals made for them.", s.Deals.Select(x => Row(x)).ToList()),
            new("records/custom-fields.json", "Values of the workspace's own fields on their records.", d.CustomFieldValues.Select(v => Row(v)).ToList()),
            new("records/relationships.json", "How their contact is related to other contacts.", d.Relationships.Select(r => Row(r)).ToList()),
            new("records/duplicate-decisions.json", "Staff's decisions that one of their records is not a duplicate of another.", d.DuplicateDecisions.Select(x => Row(x)).ToList()),
            new("timeline/activities.json", "Calls, meetings, emails and notes logged on their records.", d.Activities.Select(a => Row(a)).ToList()),
            new("timeline/notes-mentioning-them.json", "Notes and activities on other records that mention them.", d.ActivityMentions.Select(a => Row(a)).ToList()),
            new("timeline/tasks.json", "Tasks about them, with their comments.", d.Tasks.Select(t => With(Row(t), "comments",
                RowsOf(tasksById.GetValueOrDefault(t.Id)))).ToList()),
            new("timeline/tasks-mentioning-them.json", "Tasks elsewhere whose words mention them.", d.TaskMentions.Select(t => Row(t)).ToList()),
            new("timeline/lead-status-history.json", "How the status of their leads changed.", d.LeadStatusEvents.Select(e => Row(e)).ToList()),
            new("timeline/notifications.json", "Alerts staff were shown about them.", d.Notifications.Select(n => Row(n)).ToList()),
            new("mail/sent.json", "Emails sent to them, and events about them posted to the workspace's integrations.",
                d.Outbound.Select(m => With(Row(m), "bcc", OwnBlindCopy(m.Bcc, s))).ToList()),
            new("mail/sent-before-the-outbox.json", "Emails sent to them by the older email composer.",
                d.SentMessages.Select(m => With(Row(m), "bcc", OwnBlindCopy(m.Bcc, s))).ToList()),
            new("mail/received.json", "Replies received from them.", d.Inbound.Select(i => Row(i)).ToList()),
            new("mail/member-messages.json", "Club messages and campaigns sent to them, with how each went.", d.MemberMessages.Select(m => Row(m)).ToList()),
            new("outreach/sequence-enrolments.json", "Email sequences they were enrolled in.", d.Enrollments.Select(e => Row(e)).ToList()),
            new("outreach/automation-runs.json", "Automation rules that ran because of their records, with what each step did.",
                d.AutomationRuns.Select(r => With(Row(r, nameof(AutomationRun.StepsJson), nameof(AutomationRun.ClaimCount), nameof(AutomationRun.CurrentPath)),
                    "log", RowsOf(stepsByRun.GetValueOrDefault(r.Id)))).ToList()),
            new("outreach/automation-fired.json", "When a rule was set off by one of their leads or deals before runs were kept with a log: the rule and the time.",
                d.AutomationFires.Where(f => !d.AutomationRuns.Any(r => r.RuleId == f.RuleId && r.SourceKey == f.SourceKey)).Select(f => (object)Row(f)).ToList()),
            new("outreach/generated-copy.json", "Text the AI writer generated for messages to them.", d.CopyHistory.Select(c => Row(c)).ToList()),
            new("consent/consent-records.json", "Every answer they gave about being contacted, by channel and purpose, with where it came from.", d.Consent.Select(c => Row(c)).ToList()),
            new("consent/suppression-list.json", "Their addresses on the workspace's suppression list.", d.Suppressions.Select(x => Row(x)).ToList()),
            new("contracts/contracts.json", "Contracts sent to them, with the record of signatures.", d.Contracts.Select(c => With(Row(c), "events",
                RowsOf(eventsByContract.GetValueOrDefault(c.Id)))).ToList()),
            new("contracts/onboarding-handoffs.json", "Their hand-over to the member portal after a deal was won.", d.Onboardings.Select(o => Row(o)).ToList()),
            new("club/house-account.json", "Their house account: the charges and payments on it, and what each payment paid.", [new
            {
                accounts = d.HouseAccounts.Select(a => Row(a)).ToList(),
                charges = d.Charges.Select(c => Row(c)).ToList(),
                payments = d.Payments.Select(p => Row(p)).ToList(),
                allocations = d.Allocations.Select(a => Row(a)).ToList(),
            }]),
            new("club/subscriptions.json", "Their dues and other subscriptions.", d.Subscriptions.Select(x => Row(x)).ToList()),
            new("club/tabs.json", "Their bar and restaurant tabs, item by item.", d.Tabs.Select(t => With(Row(t), "items",
                RowsOf(itemsByTab.GetValueOrDefault(t.Id)))).ToList()),
            new("club/sales.json", "What they bought at the house-charge screen, line by line.", d.Sales.Select(x => With(Row(x), "lines",
                RowsOf(linesBySale.GetValueOrDefault(x.Id)))).ToList()),
            new("club/pos-tickets.json", "Tickets from the till (POS) posted to them.", d.PosTickets.Select(t => Row(t)).ToList()),
            new("club/donations.json", "Their donations.", d.Donations.Select(x => Row(x)).ToList()),
            new("club/gift-cards.json", "Gift cards they bought or were given, with every use.", d.GiftCards.Select(g => With(Row(g), "transactions",
                RowsOf(transactionsByCard.GetValueOrDefault(g.Id)))).ToList()),
            new("club/online-payments.json", "Payments online (Stripe), and the card saved for dues: only its brand, last four digits and expiry.", [new
            {
                payments = d.OnlinePayments.Select(p => Row(p)).ToList(),
                savedCards = d.PaymentProfiles.Select(p => Row(p)).ToList(),
            }]),
            new("club/bank-transfers.json", "Bank transfers matched to them.", d.BankTransactions.Select(t => Row(t)).ToList()),
            new("club/payment-references.json", "QR-bill references printed on their statements and invoices.", d.PaymentReferences.Select(r => Row(r)).ToList()),
            new("club/statements-and-reminders.json", "Statements sent to them and reminders of charges not paid.", [new
            {
                statements = d.StatementDeliveries.Select(x => Row(x)).ToList(),
                reminders = d.PaymentReminders.Select(x => Row(x)).ToList(),
            }]),
            new("club/member-codes.json", "Their member numbers, cards and QR codes at the till.", d.MemberCodes.Select(c => Row(c)).ToList()),
            new("club/member-portal.json", "When they were invited to the member portal and used it, and their sessions there (no sign-in secret is kept).", [new
            {
                use = d.PortalAccounts.Select(a => Row(a)).ToList(),
                sessions = d.PortalSessions.Select(x => Row(x)).ToList(),
                linksNotYetUsed = d.PortalLinks.Select(l => new { l.Purpose, l.CreatedAtUtc, l.ExpiresAtUtc }).ToList(),
            }]),
            new("club/events.json", "Events they registered for or brought guests to.", d.Registrations.Select(r => With(Row(r), "event",
                d.Events.TryGetValue(r.EventId, out var e) ? new { e.Name, e.StartAtUtc, e.EndAtUtc, e.Location } : null)).ToList()),
            new("club/reservations.json", "Tables and rooms they booked.", d.Reservations.Select(r => Row(r)).ToList()),
            new("club/visits.json", "Their visits to the club.", d.Visits.Select(v => Row(v)).ToList()),
            new("club/loyalty.json", "Loyalty points they earned and spent.", d.Loyalty.Select(e => Row(e)).ToList()),
            new("club/households.json", "The households they belong to, and their place in each.", d.HouseholdLinks.Select(l => With(Row(l), "household",
                d.Households.TryGetValue(l.HouseholdId, out var h) ? new { h.Name, h.Notes } : null)).ToList()),
            new("club/old-documents.json", "Club records kept read-only from before the club's records had tables of their own, that name them.", d.Documents.Select(x => Row(x)).ToList()),
            new("audit/audit-trail.json", "The history of changes to their records: who changed what, and when.", d.Audit.Select(a => Row(a)).ToList()),
        ];
    }

    /// <summary>
    /// A row as the export writes it: every column the model maps, in camel case, except
    /// what is the system's own (version stamps, token hashes, secrets, file bytes).
    /// </summary>
    private Dictionary<string, object?> Row(object entity, params string[] leaveOut)
    {
        var row = new Dictionary<string, object?>(StringComparer.Ordinal);
        var type = _db.Model.FindEntityType(entity.GetType());
        if (type == null) return row;
        foreach (var property in type.GetProperties())
        {
            if (property.IsShadowProperty() || leaveOut.Contains(property.Name) || Internal(property.Name)) continue;
            var value = property.PropertyInfo?.GetValue(entity) ?? property.FieldInfo?.GetValue(entity);
            row[JsonNamingPolicy.CamelCase.ConvertName(property.Name)] = value;
        }
        return row;
    }

    /// <summary>Columns that are the system's own workings, not anything about the person.</summary>
    private static bool Internal(string name) =>
        name is "Version" or "RowVersion" or "EditVersion" or "Content" or "UnsubscribeUrl" or "CheckoutUrl"
            or "IdempotencyKey" or "AttemptKey" or "ClaimedUntilUtc" or "ApprovalClaimedUntilUtc" or "ReplyToken"
            or "WebhookKey" or "PublicToken"
        || name.EndsWith("Hash", StringComparison.Ordinal)
        || name.EndsWith("Protected", StringComparison.Ordinal);

    /// <summary>
    /// The blind copies of an email as the person may see them: their own address, when they
    /// were one. The others were copied without the person being shown, and an access request
    /// does not hand over other people's data they never saw.
    /// </summary>
    private static string? OwnBlindCopy(string? bcc, PersonSubject s)
    {
        var own = (bcc ?? "").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(entry => PersonSubject.Address(entry) is var address && (s.Addresses.Contains(address) || s.SharedAddresses.Contains(address)))
            .ToList();
        return own.Count == 0 ? null : string.Join(", ", own);
    }

    private List<object> RowsOf<T>(IEnumerable<T>? items) where T : class =>
        items?.Select(i => (object)Row(i)).ToList() ?? new List<object>();

    private static Dictionary<string, object?> With(Dictionary<string, object?> row, string key, object? value)
    {
        row[key] = value;
        return row;
    }

    private static int Items(IEnumerable<ExportSection> sections, PersonDossier d) =>
        sections.Sum(s => s.Rows.Count) + d.Attachments.Count;

    /// <summary>The readable summary at the top of the zip.</summary>
    private static string Summary(string workspace, string actor, DateTime now, PersonDossier d, IReadOnlyList<ExportSection> sections)
    {
        var s = d.Subject;
        var text = new StringBuilder();
        text.AppendLine($"Personal data held by {workspace}");
        text.AppendLine($"Exported on {now.ToString("d MMMM yyyy 'at' HH:mm 'UTC'", CultureInfo.InvariantCulture)} by {actor}.");
        text.AppendLine();
        text.AppendLine("This export holds everything the workspace keeps in Cadence about the person below: their records, their");
        text.AppendLine("history, the emails sent to them and received from them, their consent, and their club account. Each file is");
        text.AppendLine("JSON (it opens in any text editor); the files attached to their records are in the folder \"files\".");
        text.AppendLine();
        text.AppendLine("The person's records");
        foreach (var m in s.Members) text.AppendLine($"  Member       {m.FirstName} {m.LastName} {Bracket(m.Email)}  ({m.Tier}, {m.Status})");
        foreach (var c in s.Contacts) text.AppendLine($"  Contact      {c.Name} {Bracket(c.Email)}");
        foreach (var l in s.Leads) text.AppendLine($"  Lead         {l.Name} {Bracket(l.Email)}  ({(l.IsConverted ? "converted" : l.Status)})");
        foreach (var a in s.Applications) text.AppendLine($"  Application  {a.FirstName} {a.LastName} {Bracket(a.Email)}  ({a.Status})");
        if (s.RecordCount == 0) text.AppendLine("  None: the workspace holds no record under this address, only what is listed below.");
        text.AppendLine();
        if (s.Addresses.Count > 0) text.AppendLine($"Email addresses searched: {string.Join(", ", s.Addresses.OrderBy(a => a))}");
        if (s.SharedAddresses.Count > 0)
            text.AppendLine($"Also used by someone else, so searched only through the records above: {string.Join(", ", s.SharedAddresses.OrderBy(a => a))}");
        text.AppendLine();
        text.AppendLine("What is in this export");
        foreach (var section in sections.Where(x => x.Rows.Count > 0))
            text.AppendLine($"  {section.Path,-40} {Count(section),6}   {section.About}");
        if (d.Attachments.Count > 0)
            text.AppendLine($"  {"files/",-40} {d.Attachments.Count,6}   Files attached to their records.");
        text.AppendLine();
        text.AppendLine("Not in this export");
        text.AppendLine("  Emails to other people that mention them: those are other people's correspondence. They are");
        text.AppendLine("  rewritten without the mention if this person is erased.");
        text.AppendLine("  Copies held by the services the workspace uses: the mail service (SendGrid), Stripe for card");
        text.AppendLine("  payments, the till (Lightspeed), the bank, and the accounting software the club exports to. Ask the");
        text.AppendLine("  club for those, or ask the service directly.");
        return text.ToString();
    }

    /// <summary>How many records a file holds, for the summary.</summary>
    private static int Count(ExportSection section) =>
        section.Rows.Count == 1 && section.Rows[0] is not Dictionary<string, object?>
            ? section.Rows[0].GetType().GetProperties().Sum(p => p.GetValue(section.Rows[0]) is System.Collections.ICollection c ? c.Count : 0)
            : section.Rows.Count;

    private static string Bracket(string? email) => string.IsNullOrWhiteSpace(email) ? "" : $"<{email.Trim()}>";

    /// <summary>The record an audit line names the person through: a member first, then a contact, a lead, an application.</summary>
    private static (string? Type, Guid? Id, string Label) Primary(PersonSubject s)
    {
        if (s.Members.FirstOrDefault() is { } m) return ("member record", m.Id, $"{m.FirstName} {m.LastName}".Trim());
        if (s.Contacts.FirstOrDefault() is { } c) return ("contact", c.Id, c.Name);
        if (s.Leads.FirstOrDefault() is { } l) return ("lead", l.Id, l.Name);
        if (s.Applications.FirstOrDefault() is { } a) return ("application", a.Id, $"{a.FirstName} {a.LastName}".Trim());
        return (null, null, "An address with no records");
    }

    private static string SafeFileName(string name)
    {
        var cleaned = new string((name ?? "file").Select(ch => Path.GetInvalidFileNameChars().Contains(ch) || ch is '/' or '\\' ? '_' : ch).ToArray()).Trim();
        return cleaned.Length == 0 ? "file" : cleaned.Length > 120 ? cleaned[..120] : cleaned;
    }

    private static string Plural(int n, string word) => n == 1 ? word : word + "s";

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
}
