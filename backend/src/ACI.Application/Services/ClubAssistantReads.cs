namespace ACI.Application.Services;

/// <summary>
/// What the assistant may read about the club, as short text it hands the language model: the
/// members, today at the club, who owes money, renewals coming up, events and the bookable
/// resources. Every method reads one workspace's records and nobody else's.
/// </summary>
/// <remarks>
/// Club records are read by anyone in the workspace on the club's own pages (only changing them
/// takes a permission), so the assistant reads them for anyone in it too, while the Club module
/// is on. A member whose personal data was erased keeps a renamed row for the books; it is left
/// out of everything here, as it is from anything that would show or contact the person.
/// </remarks>
public interface IClubAssistantReads
{
    /// <summary>Members whose name or email holds <paramref name="query"/> (all, newest first, without one).</summary>
    Task<string> SearchMembersAsync(Guid organizationId, string? query, int limit, CancellationToken ct = default);

    /// <summary>One member's membership, account and coming bookings; null when not in this workspace.</summary>
    Task<string?> MemberAsync(Guid organizationId, Guid memberId, CancellationToken ct = default);

    /// <summary>The club's day: check-ins, today's bookings, waiting applications, what is past due, this week's events.</summary>
    Task<string> TodayAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>Active members whose membership renews within <paramref name="days"/> days.</summary>
    Task<string> RenewalsDueAsync(Guid organizationId, int days, CancellationToken ct = default);

    /// <summary>Published events starting within <paramref name="days"/> days, with their seats taken.</summary>
    Task<string> UpcomingEventsAsync(Guid organizationId, int days, CancellationToken ct = default);

    /// <summary>What can be booked: rooms, tables, courts, with their ids.</summary>
    Task<string> ResourcesAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>A member a proposal names: null when not in this workspace, or erased.</summary>
    Task<ClubMemberRef?> MemberRefAsync(Guid organizationId, Guid memberId, CancellationToken ct = default);

    /// <summary>A resource a proposed booking names, with the deposit booking it takes: null when not in this workspace.</summary>
    Task<ClubResourceRef?> ResourceRefAsync(Guid organizationId, Guid resourceId, CancellationToken ct = default);

    /// <summary>The workspace's currency (house accounts are kept in it) and time zone.</summary>
    Task<(string Currency, TimeZoneInfo Zone)> WorkspaceAsync(Guid organizationId, CancellationToken ct = default);
}

public sealed record ClubMemberRef(Guid Id, string Name, string Status, string Tier);

/// <param name="DepositAmount">What booking it charges to the member's house account until they come (0 for none).</param>
public sealed record ClubResourceRef(Guid Id, string Name, string Kind, int Capacity, bool Active, decimal DepositAmount, string Currency);
