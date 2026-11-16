using System.Text;
using ACI.Application.Common;
using ACI.Application.Common.I18n;
using ACI.Application.DTOs;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Infrastructure.Services.Retention;

// What a member's crossing into at risk sets off, by the club's rules: a task for the member's
// owner, an email to the member through the outbox (each off until the club switches it on), and
// the automation trigger "When a member becomes at risk" for the rules the club made. Only the
// crossing claimed first gets here, and not again within the club's cooldown.
public sealed partial class ClubRetentionService
{
    /// <summary>The placeholders the club's own email text may use, filled here before the outbox sees it.</summary>
    internal const string FirstNamePlaceholder = "{{firstName}}";
    internal const string NamePlaceholder = "{{name}}";
    internal const string ClubPlaceholder = "{{club}}";

    private async Task SaveActionsAsync(Guid organizationId, MemberRow member, ClubMemberRisk row, RiskResult result, ClubRetentionSettings settings,
        string clubName, Guid workspaceOwner, Guid actorUserId, DateTime now, Tally tally, CancellationToken ct)
    {
        // A job acts as the workspace's owner (no person pressed anything); a recompute as whoever pressed it.
        var actor = actorUserId != Guid.Empty ? actorUserId : workspaceOwner;
        row.SaveActionsAtUtc = now;
        row.SaveTaskId = null;
        row.SaveMessageId = null;
        var owner = await OwnerOfAsync(organizationId, member.OwnerUserId, workspaceOwner, ct);
        var signs = result.Factors.Where(f => f.Points > 0).OrderByDescending(f => f.Points).Take(3).Select(f => f.Detail).ToList();

        if (settings.SaveTask)
        {
            var created = await _tasks.CreateAsync(actor, organizationId, new CreateTaskRequest
            {
                Title = Clip($"Member at risk: {member.Name}", 200),
                Description = Clip($"About {member.Name}: their at-risk score is {result.Score}, at or over the club's threshold of {settings.AtRiskThreshold}. "
                    + string.Join(" ", signs), 2000),
                DueDateUtc = now.AddDays(settings.SaveTaskDueDays),
                Priority = "high",
                // A member has no task link of their own: the task sits on their contact when they have one.
                ContactId = member.ContactId,
                AssigneeId = owner,
                MadeBy = "the club's retention rules",
            }, ct);
            if (created.IsSuccess)
            {
                row.SaveTaskId = created.Value.Id;
                tally.Tasks++;
                tally.Notes.Add($"A task was made for {member.Name}'s owner.");
            }
            else tally.Notes.Add($"No task was made: {created.Error.Description}");
        }

        if (settings.SaveEmail)
        {
            var queued = await EmailMemberAsync(organizationId, member, settings, clubName, actor, owner, ct);
            if (queued != null)
            {
                row.SaveMessageId = queued;
                tally.Emails++;
                tally.Notes.Add($"An email to {member.Name} was queued.");
            }
        }
        await _db.SaveChangesAsync(ct);

        // The club's own rules on the trigger, once per crossing (the key names the moment). A rule
        // that fails cannot undo the score, or the task and email above.
        if (_automations != null)
        {
            try
            {
                await _automations.DispatchAsync(AutomationCatalog.MemberAtRisk, new AutomationEvent(
                    organizationId, actor, $"{member.Id}:{now:yyyyMMddHHmmss}", member.Name, Email: member.Email, MemberId: member.Id), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Member {MemberId} became at risk, but the workspace's automations for it failed", member.Id);
            }
        }
    }

    /// <summary>
    /// Queues the save email on the outbox, logs it on the member's communications, and answers
    /// the outbox message, or null when it was not queued (no address, no consent to marketing,
    /// the suppression list), which the log says.
    /// </summary>
    private async Task<Guid?> EmailMemberAsync(Guid organizationId, MemberRow member, ClubRetentionSettings settings, string clubName, Guid actor,
        Guid? owner, CancellationToken ct)
    {
        var address = (member.Email ?? "").Trim();
        // In the member's language: the standard words exist in English, German and French.
        var language = AppLanguages.ForMember(member.PreferredLanguage);
        var club = string.IsNullOrWhiteSpace(clubName) ? MemberTexts.T(language, "theClub") : clubName.Trim();
        var firstName = string.IsNullOrWhiteSpace(member.FirstName) ? member.Name : member.FirstName.Trim();
        string Fill(string text) => text.Replace(FirstNamePlaceholder, firstName).Replace(NamePlaceholder, member.Name).Replace(ClubPlaceholder, club);
        var subject = settings.SaveEmailSubject is { } ownSubject ? Fill(ownSubject) : MemberTexts.T(language, "retention.subject", ("club", club));
        var body = settings.SaveEmailBody is { } ownBody
            ? Fill(ownBody)
            : new StringBuilder()
                .Append(MemberTexts.T(language, "greeting", ("name", firstName))).Append("\n\n")
                .Append(MemberTexts.T(language, "retention.body", ("club", club))).Append("\n\n")
                .Append(MemberTexts.T(language, "retention.closing")).Append("\n\n")
                .Append(club)
                .ToString();
        var log = new MemberMessage
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            MemberId = member.Id,
            MemberName = member.Name,
            Email = address.Length == 0 ? null : address,
            Channel = CampaignChannels.Email,
            Category = "Other",
            Purpose = settings.SaveEmailPurpose,
            Subject = Clip(subject, 200),
            Preview = Clip(body, 500),
            Status = MemberMessageStatuses.Queued,
            CreatedByUserId = actor,
            CreatedAtUtc = DateTime.UtcNow,
        };
        _db.MemberMessages.Add(log);
        if (!ValidationHelper.IsValidEmail(address))
        {
            log.Status = MemberMessageStatuses.Skipped;
            log.Reason = "The member has no valid email address.";
            return null;
        }
        // Marketing goes only to a member who agreed to it by email; the outbox checks, and adds
        // the link that withdraws it. A service email goes whatever they chose.
        var queued = await _outbox.QueueEmailAsync(new OutboundEmailRequest
        {
            OrganizationId = organizationId,
            Source = OutboundSources.Retention,
            SourceId = member.Id,
            To = [address],
            RecipientName = member.Name,
            Subject = subject,
            Body = body,
            CreatedByUserId = actor,
            ReplyToUserId = owner ?? actor,
            MemberId = member.Id,
            Purpose = settings.SaveEmailPurpose,
        }, ct);
        if (queued.IsSuccess)
        {
            log.OutboundMessageId = queued.Value.Id;
            return queued.Value.Id;
        }
        log.Status = MemberMessageStatuses.Suppressed;
        log.Reason = Clip(queued.Error.Description, 500);
        return null;
    }

    /// <summary>The member's owner while they are in the workspace, else the workspace's owner; null when neither is.</summary>
    private async Task<Guid?> OwnerOfAsync(Guid organizationId, Guid ownerUserId, Guid workspaceOwner, CancellationToken ct)
    {
        foreach (var candidate in new[] { ownerUserId, workspaceOwner })
            if (candidate != Guid.Empty && await _organizations.GetMemberRoleAsync(candidate, organizationId, ct) != null)
                return candidate;
        return null;
    }
}
