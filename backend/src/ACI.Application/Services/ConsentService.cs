using System.Globalization;
using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

/// <summary>How one channel and purpose stands for a person: granted, withdrawn, or never asked.</summary>
public static class ConsentStates
{
    public const string Granted = "granted";
    public const string Withdrawn = "withdrawn";
    public const string Unknown = "unknown";
}

/// <summary>Where one channel and purpose stands, and what put it there.</summary>
public sealed record ConsentStateDto(
    string Channel, string Purpose, string State, string? Source, DateTime? RecordedAtUtc, string? RecordedByName, string? Detail);

/// <summary>One answer as recorded, for the member page's history.</summary>
public sealed record ConsentRecordDto(
    Guid Id, string Channel, string Purpose, bool Granted, string Source, string? Address, string? Detail,
    DateTime RecordedAtUtc, string? RecordedByName, bool IsCurrent);

/// <summary>A member's consent: where each channel and purpose stands, and every answer recorded.</summary>
public sealed record MemberConsentDto(Guid MemberId, IReadOnlyList<ConsentStateDto> Current, IReadOnlyList<ConsentRecordDto> History);

/// <summary>A consent answer staff record for a member.</summary>
public sealed record RecordConsentRequest
{
    /// <summary>email, sms or post.</summary>
    public string? Channel { get; init; }

    /// <summary>marketing or service.</summary>
    public string? Purpose { get; init; }

    /// <summary>Whether the member agreed (true) or refused or withdrew (false).</summary>
    public bool? Granted { get; init; }

    /// <summary>form, member or staff: how it was given.</summary>
    public string? Source { get; init; }

    /// <summary>What was agreed and how: the form's wording, where the paper is kept, what was said.</summary>
    public string? Detail { get; init; }
}

public interface IConsentService
{
    /// <summary>The member's consent, or null when the member is not one of the workspace's.</summary>
    Task<MemberConsentDto?> ForMemberAsync(Guid organizationId, Guid memberId, CancellationToken ct = default);

    /// <summary>Records an answer staff were given; the history keeps the one it replaces.</summary>
    Task<Result<MemberConsentDto>> RecordForMemberAsync(Guid organizationId, Guid memberId, Guid userId, RecordConsentRequest request, CancellationToken ct = default);

    /// <summary>
    /// Records an answer the member gave themselves in the member portal: source member, recorded
    /// by nobody on the club's side, with the address it was given at. The history keeps the one
    /// it replaces.
    /// </summary>
    Task<Result<MemberConsentDto>> RecordByMemberAsync(Guid organizationId, Guid memberId, string? channel, string? purpose, bool? granted, CancellationToken ct = default);

    /// <summary>Which of these members agreed to <paramref name="purpose"/> on <paramref name="channel"/> and have not withdrawn it.</summary>
    Task<IReadOnlySet<Guid>> MembersWhoAgreedAsync(Guid organizationId, IReadOnlyCollection<Guid> memberIds, string channel, string purpose, CancellationToken ct = default);

    /// <summary>
    /// Records that marketing by email is withdrawn for the member a link names and for every
    /// other member of the workspace at that address, or with an answer given at it, as given
    /// by the person themselves (<paramref name="source"/>). Returns how many members it
    /// changed; a member already withdrawn is left as they are, so a link followed twice
    /// records it once.
    /// </summary>
    Task<int> WithdrawMarketingEmailAsync(Guid organizationId, Guid? memberId, string address, string source, string? detail, CancellationToken ct = default);
}

/// <summary>
/// Consent per channel and purpose, for club members: what staff record on the member page,
/// what an unsubscribe link withdraws, and what sending reads before marketing goes out.
/// </summary>
/// <remarks>
/// Marketing goes only to someone whose standing answer for that channel is yes. No answer
/// at all is no: the club has to be able to show the person agreed. Service messages
/// (statements, booking confirmations, receipts) do not ask; the member's answer for them is
/// kept as they gave it.
/// </remarks>
public sealed class ConsentService : IConsentService, IMemberMailRules
{
    /// <summary>The history the member page shows: enough for years of changes.</summary>
    private const int HistoryShown = 200;

    private const int MaxDetail = 500;

    private readonly IConsentRepository _consent;
    private readonly IClubRepository _club;
    private readonly IUserRepository _users;

    public ConsentService(IConsentRepository consent, IClubRepository club, IUserRepository users)
    {
        _consent = consent;
        _club = club;
        _users = users;
    }

    public async Task<MemberConsentDto?> ForMemberAsync(Guid organizationId, Guid memberId, CancellationToken ct = default)
    {
        var member = await _club.MemberAsync(memberId, organizationId, ct);
        if (member == null) return null;
        return await DtoAsync(organizationId, memberId, ct);
    }

    public async Task<Result<MemberConsentDto>> RecordForMemberAsync(Guid organizationId, Guid memberId, Guid userId, RecordConsentRequest request, CancellationToken ct = default)
    {
        var channel = (request.Channel ?? "").Trim().ToLowerInvariant();
        if (!ConsentChannels.All.Contains(channel))
            return new Error("Consent.ChannelInvalid", "Consent is recorded for email, SMS or post.");
        var purpose = (request.Purpose ?? "").Trim().ToLowerInvariant();
        if (!ConsentPurposes.All.Contains(purpose))
            return new Error("Consent.PurposeInvalid", "Consent is recorded for marketing or for service messages.");
        if (request.Granted is not bool granted)
            return new Error("Consent.AnswerRequired", "Say whether the member agreed or not.");
        var source = (request.Source ?? "").Trim().ToLowerInvariant();
        if (!ConsentSources.Recordable.Contains(source))
            return new Error("Consent.SourceInvalid", "Say how it was given: on a form, by the member, or by staff.");
        var detail = string.IsNullOrWhiteSpace(request.Detail) ? null : request.Detail.Trim();
        if (detail?.Length > MaxDetail)
            return new Error("Consent.TooLong", $"The note can be at most {MaxDetail} characters.");

        var member = await _club.MemberAsync(memberId, organizationId, ct);
        if (member == null) return DomainErrors.Club.MemberNotFound;

        var record = new ConsentRecord
        {
            OrganizationId = organizationId,
            SubjectType = ConsentSubjects.Member,
            SubjectId = memberId,
            Channel = channel,
            Purpose = purpose,
            Granted = granted,
            Source = source,
            Address = AddressFor(member, channel),
            Detail = detail,
            RecordedByUserId = userId,
        };
        if (!await AddAsync(record, ct))
            return new Error("Consent.Changed.Conflict", "Someone else recorded this member's consent at the same moment. Reload and check it.");
        return await DtoAsync(organizationId, memberId, ct);
    }

    public async Task<Result<MemberConsentDto>> RecordByMemberAsync(Guid organizationId, Guid memberId, string? channel, string? purpose, bool? granted, CancellationToken ct = default)
    {
        var on = (channel ?? "").Trim().ToLowerInvariant();
        if (!ConsentChannels.All.Contains(on))
            return new Error("Consent.ChannelInvalid", "Consent is recorded for email, SMS or post.");
        var forWhat = (purpose ?? "").Trim().ToLowerInvariant();
        if (!ConsentPurposes.All.Contains(forWhat))
            return new Error("Consent.PurposeInvalid", "Consent is recorded for marketing or for service messages.");
        if (granted is not bool yes)
            return new Error("Consent.AnswerRequired", "Say whether you agree or not.");
        var member = await _club.MemberAsync(memberId, organizationId, ct);
        if (member == null || member.ErasedAtUtc != null) return DomainErrors.Club.MemberNotFound;

        var record = new ConsentRecord
        {
            OrganizationId = organizationId,
            SubjectType = ConsentSubjects.Member,
            SubjectId = memberId,
            Channel = on,
            Purpose = forWhat,
            Granted = yes,
            Source = ConsentSources.Member,
            Address = AddressFor(member, on),
            Detail = "Set by the member in the member portal.",
            RecordedByUserId = null,
        };
        if (!await AddAsync(record, ct))
            return new Error("Consent.Changed.Conflict", "Your choice changed somewhere else at the same moment. Reload and check it.");
        return await DtoAsync(organizationId, memberId, ct);
    }

    public async Task<IReadOnlySet<Guid>> MembersWhoAgreedAsync(Guid organizationId, IReadOnlyCollection<Guid> memberIds, string channel, string purpose, CancellationToken ct = default)
    {
        var current = await _consent.CurrentAsync(organizationId, ConsentSubjects.Member, memberIds, channel, purpose, ct);
        return current.Where(c => c.Granted).Select(c => c.SubjectId).ToHashSet();
    }

    public async Task<int> WithdrawMarketingEmailAsync(Guid organizationId, Guid? memberId, string address, string source, string? detail, CancellationToken ct = default)
    {
        var normalized = Normalize(address);
        // The person who followed the link holds that mailbox: their "stop" is for every
        // member the club writes to there (a couple sharing one address, a duplicate record),
        // not only the record the link was made for. That includes a member with an answer
        // given at the address. Two records of one person merged: the one merged away is
        // gone, and the link in an email sent to it names a member who no longer exists,
        // but its answers (and the address they were given at) are the kept member's now,
        // and so is the marketing that link has to stop, wherever it is sent.
        var answeredHere = normalized.Length == 0
            ? new HashSet<Guid>()
            : (await _consent.SubjectsAtAddressAsync(organizationId, ConsentSubjects.Member, ConsentChannels.Email, normalized, ct)).ToHashSet();
        // Members of the workspace only: an answer written for a member who has gone (merged
        // away, or removed at their request) would only bring their address back.
        var members = (await _club.MembersAsync(organizationId, ct))
            .Where(m => m.Id == memberId || (normalized.Length > 0 && (Normalize(m.Email) == normalized || answeredHere.Contains(m.Id))))
            .Select(m => m.Id)
            .ToList();
        if (members.Count == 0) return 0;

        var standing = (await _consent.CurrentAsync(organizationId, ConsentSubjects.Member, members, ConsentChannels.Email, ConsentPurposes.Marketing, ct))
            .ToDictionary(c => c.SubjectId);
        var changed = 0;
        foreach (var id in members)
        {
            if (standing.TryGetValue(id, out var now) && !now.Granted) continue;
            var added = await AddAsync(new ConsentRecord
            {
                OrganizationId = organizationId,
                SubjectType = ConsentSubjects.Member,
                SubjectId = id,
                Channel = ConsentChannels.Email,
                Purpose = ConsentPurposes.Marketing,
                Granted = false,
                Source = source,
                Address = normalized.Length == 0 ? null : normalized,
                Detail = detail,
            }, ct);
            if (added) changed++;
        }
        return changed;
    }

    // ── What the outbox asks ──────────────────────────────────────────────────

    public async Task<bool> MemberExistsAsync(Guid organizationId, Guid memberId, CancellationToken ct = default) =>
        await _club.MemberAsync(memberId, organizationId, ct) != null;

    public async Task<string?> PreferredLanguageAsync(Guid organizationId, Guid memberId, CancellationToken ct = default) =>
        (await _club.MemberAsync(memberId, organizationId, ct))?.PreferredLanguage;

    public async Task<MailRefusal?> MarketingRefusalAsync(Guid organizationId, Guid memberId, string channel, CancellationToken ct = default)
    {
        var standing = (await _consent.CurrentAsync(organizationId, ConsentSubjects.Member, [memberId], channel, ConsentPurposes.Marketing, ct))
            .FirstOrDefault();
        var by = ChannelName(channel);
        if (standing == null)
            return new MailRefusal("Consent.Missing", $"This member has not agreed to marketing by {by}, so nothing was sent.");
        if (standing.Granted) return null;
        var on = standing.RecordedAtUtc.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        return new MailRefusal("Consent.Withdrawn", standing.Source == ConsentSources.Unsubscribe
            ? $"This member unsubscribed from marketing by {by} on {on}, so nothing was sent."
            : $"This member withdrew consent to marketing by {by} on {on}, so nothing was sent.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds the answer as the one that stands. Two answers for the same person, channel
    /// and purpose recorded at the same moment: the unique index takes the first, and this
    /// one is tried once more on top of it, so both stay in the history and the later
    /// one stands.
    /// </summary>
    private async Task<bool> AddAsync(ConsentRecord record, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            record.Id = Guid.NewGuid();
            record.RecordedAtUtc = DateTime.UtcNow;
            if (await _consent.AddCurrentAsync(record, ct)) return true;
        }
        return false;
    }

    private async Task<MemberConsentDto> DtoAsync(Guid organizationId, Guid memberId, CancellationToken ct)
    {
        var history = await _consent.HistoryAsync(organizationId, ConsentSubjects.Member, memberId, HistoryShown, ct);
        var names = new Dictionary<Guid, string?>();
        foreach (var userId in history.Where(h => h.RecordedByUserId != null).Select(h => h.RecordedByUserId!.Value).Distinct())
            names[userId] = (await _users.GetByIdAsync(userId, ct))?.Name;
        string? NameOf(Guid? id) => id is Guid known ? names.GetValueOrDefault(known) : null;

        var current = (await _consent.CurrentAsync(organizationId, ConsentSubjects.Member, [memberId], ct: ct))
            .ToDictionary(c => (c.Channel, c.Purpose));
        var states = new List<ConsentStateDto>();
        foreach (var channel in ConsentChannels.All)
        foreach (var purpose in ConsentPurposes.All)
        {
            states.Add(current.TryGetValue((channel, purpose), out var c)
                ? new ConsentStateDto(channel, purpose, c.Granted ? ConsentStates.Granted : ConsentStates.Withdrawn,
                    c.Source, c.RecordedAtUtc, NameOf(c.RecordedByUserId), c.Detail)
                : new ConsentStateDto(channel, purpose, ConsentStates.Unknown, null, null, null, null));
        }
        return new MemberConsentDto(memberId, states, history.Select(h => new ConsentRecordDto(
            h.Id, h.Channel, h.Purpose, h.Granted, h.Source, h.Address, h.Detail, h.RecordedAtUtc, NameOf(h.RecordedByUserId), h.IsCurrent)).ToList());
    }

    private static string? AddressFor(ClubMember member, string channel) => channel switch
    {
        ConsentChannels.Email => Normalize(member.Email) is { Length: > 0 } email ? email : null,
        ConsentChannels.Sms => string.IsNullOrWhiteSpace(member.Phone) ? null : member.Phone.Trim(),
        _ => null,
    };

    private static string ChannelName(string channel) => channel switch
    {
        ConsentChannels.Sms => "SMS",
        ConsentChannels.Post => "post",
        _ => "email",
    };

    private static string Normalize(string? email) => (email ?? "").Trim().ToLowerInvariant();
}
