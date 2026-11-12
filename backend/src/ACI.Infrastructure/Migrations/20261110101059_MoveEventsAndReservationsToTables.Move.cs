namespace ACI.Infrastructure.Migrations
{
    /// <summary>
    /// The copy of the club's event and booking documents (ClubDocuments of kind events,
    /// event-attendees, resources and reservations) into the typed tables this migration makes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In the migration rather than a start-up data fix, so the tables and what is copied into
    /// them are one transaction, run once per database by the migration history: a copy that
    /// fails leaves no tables behind, rather than an events page the app would read as empty.
    /// It copies: the documents stay, unread, until a later release removes them. Every row
    /// keeps its document's id and is written only when no row has that id, so running it
    /// again adds nothing. A document whose payload is not a JSON object, or that belongs to
    /// no organization (the old personal bucket every user without one shared), stays behind.
    /// </para>
    /// <para>
    /// What the documents held comes over as it was. An event keeps its counts: the seats and
    /// waitlist places its RSVPs from the events list took without a name are kept as unnamed
    /// seats, so the event is not sold past its capacity after the move. An attendee becomes a
    /// registration marked as moved, with no ticket charge (none was ever posted), and keeps
    /// its member only when that member is still in the workspace. A resource gets the booking
    /// mode its kind has (a dining room, a lounge or a studio is shared, the rest are booked
    /// one at a time) and no deposit or no-show fee. A booking finds its member by the member
    /// id it carries, or else by its email when exactly one member of the workspace has that
    /// address; otherwise it stays a booking under a name, as it was.
    /// </para>
    /// <para>
    /// Frozen with the migration on purpose: it reads the documents as they were written up to
    /// this release, and nothing later should change what it did.
    /// </para>
    /// </remarks>
    public partial class MoveEventsAndReservationsToTables
    {
        // A date as the screens and the server wrote them (ISO 8601, mostly with Z), as UTC; null when it does not read as one.
        private static string Utc(string column) => $"CONVERT(datetime2, SWITCHOFFSET(TRY_CONVERT(datetimeoffset, {column}, 127), '+00:00'))";

        // An amount of money, to the cent; null when it is not a number, never below 0.
        private static string Money(string column) =>
            $"CASE WHEN TRY_CAST({column} AS decimal(18,2)) > 0 AND TRY_CAST({column} AS decimal(18,2)) < 1000000000 THEN TRY_CAST({column} AS decimal(18,2)) ELSE 0 END";

        // A count; 0 when it is not a whole number or is below 0.
        private static string Count(string column) =>
            $"CASE WHEN TRY_CAST({column} AS int) > 0 THEN TRY_CAST({column} AS int) ELSE 0 END";

        private static string Text(string column, int max) => $"LEFT(NULLIF(LTRIM(RTRIM({column})), N''), {max})";

        // The payload when it is a JSON object, else null, which OPENJSON reads as no rows.
        private const string JsonObject =
            "CROSS APPLY (SELECT CASE WHEN ISJSON(d.[Payload]) = 1 AND LEFT(LTRIM(d.[Payload]), 1) = N'{' THEN d.[Payload] END AS [Json]) p";

        private const string Currency =
            "CASE WHEN LEN(o.[Currency]) = 3 THEN UPPER(o.[Currency]) ELSE N'CHF' END";

        // An attendee document's field, read without failing on a payload that is not JSON.
        private static string AttendeeField(string field) =>
            $"JSON_VALUE(CASE WHEN ISJSON(a.[Payload]) = 1 THEN a.[Payload] END, '$.{field}')";

        // The attendee documents for the event document d, in the same workspace, in the given statuses.
        private static string NamedAttendees(string statuses) => $@"
            SELECT COUNT(*) FROM [ClubDocuments] a
            WHERE a.[Kind] = N'event-attendees' AND a.[OrganizationId] = d.[OrganizationId]
              AND TRY_CONVERT(uniqueidentifier, {AttendeeField("eventId")}) = d.[Id]
              AND COALESCE({AttendeeField("status")}, N'Reserved') IN ({statuses})";

        /// <summary>The copy, on its own, so a test can run it a second time and see it add nothing.</summary>
        public static readonly string MoveDocumentsSql = $@"
SET NOCOUNT ON;
DECLARE @empty uniqueidentifier = '00000000-0000-0000-0000-000000000000';

-- ── events ───────────────────────────────────────────────────────────────────
INSERT INTO [ClubEvents] ([Id], [OrganizationId], [Name], [Description], [Location], [StartAtUtc], [EndAtUtc], [Capacity],
    [MemberPrice], [GuestPrice], [Currency], [NoShowFee], [Visibility], [Status], [CoverEmoji], [UnnamedSeats], [UnnamedWaitlist],
    [CreatedAtUtc], [CreatedByUserId], [CreatedByName], [UpdatedAtUtc], [ListChangedAtUtc], [EditVersion], [Version])
SELECT d.[Id], d.[OrganizationId],
    LEFT(COALESCE({Text("j.[name]", 200)}, N'Event'), 200),
    {Text("j.[description]", 4000)},
    {Text("j.[location]", 200)},
    s.[StartAtUtc],
    CASE WHEN {Utc("j.[endAtUtc]")} > s.[StartAtUtc] THEN {Utc("j.[endAtUtc]")} ELSE DATEADD(hour, 2, s.[StartAtUtc]) END,
    {Count("j.[capacity]")},
    {Money("j.[memberPrice]")}, {Money("j.[guestPrice]")}, {Currency}, 0,
    CASE WHEN j.[visibility] IN (N'MembersOnly', N'MembersAndGuests', N'Public') THEN j.[visibility] ELSE N'MembersAndGuests' END,
    CASE WHEN j.[status] IN (N'Draft', N'Published', N'Cancelled', N'Completed') THEN j.[status] ELSE N'Draft' END,
    LEFT(COALESCE(NULLIF(LTRIM(RTRIM(j.[coverEmoji])), N''), N'🎉'), 16),
    CASE WHEN {Count("j.[registeredCount]")} > named.[Seats] THEN {Count("j.[registeredCount]")} - named.[Seats] ELSE 0 END,
    CASE WHEN {Count("j.[waitlistCount]")} > named.[Waiting] THEN {Count("j.[waitlistCount]")} - named.[Waiting] ELSE 0 END,
    d.[UpdatedAtUtc], NULL, NULL, d.[UpdatedAtUtc], NULL, NEWID(), NEWID()
FROM [ClubDocuments] d
{JsonObject}
CROSS APPLY OPENJSON(p.[Json]) WITH (
    [name] nvarchar(max) '$.name', [description] nvarchar(max) '$.description', [location] nvarchar(max) '$.location',
    [startAtUtc] nvarchar(max) '$.startAtUtc', [endAtUtc] nvarchar(max) '$.endAtUtc', [capacity] nvarchar(max) '$.capacity',
    [registeredCount] nvarchar(max) '$.registeredCount', [waitlistCount] nvarchar(max) '$.waitlistCount',
    [memberPrice] nvarchar(max) '$.memberPrice', [guestPrice] nvarchar(max) '$.guestPrice',
    [visibility] nvarchar(max) '$.visibility', [status] nvarchar(max) '$.status', [coverEmoji] nvarchar(max) '$.coverEmoji') j
LEFT JOIN [Organizations] o ON o.[Id] = d.[OrganizationId]
CROSS APPLY (SELECT COALESCE({Utc("j.[startAtUtc]")}, d.[UpdatedAtUtc]) AS [StartAtUtc]) s
CROSS APPLY (SELECT ({NamedAttendees("N'Reserved', N'CheckedIn'")}) AS [Seats], ({NamedAttendees("N'Waitlisted'")}) AS [Waiting]) named
WHERE d.[Kind] = N'events' AND d.[OrganizationId] IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM [ClubEvents] x WHERE x.[Id] = d.[Id]);

-- ── who is coming ────────────────────────────────────────────────────────────
INSERT INTO [ClubEventRegistrations] ([Id], [OrganizationId], [EventId], [MemberId], [Name], [Email], [TicketTier], [HostMemberId],
    [HostName], [Status], [Source], [RegisteredAtUtc], [PromotedAtUtc], [CheckedInAtUtc], [CheckedInByUserId], [CheckedInByName],
    [NoShowAtUtc], [CancelledAtUtc], [CancelledByUserId], [CancelledByName], [CancelReason], [TicketPrice], [Currency], [ChargeId],
    [ChargeReversedAtUtc], [NoShowFee], [NoShowChargeId], [NoShowFeeReversedAtUtc], [PointsAwarded], [LoyaltyEntryId],
    [ImportedFromId], [CreatedAtUtc], [CreatedByUserId], [CreatedByName], [UpdatedAtUtc])
SELECT d.[Id], d.[OrganizationId], ev.[Id], m.[Id],
    LEFT(COALESCE({Text("j.[memberName]", 200)}, NULLIF(LTRIM(RTRIM(m.[FirstName] + N' ' + m.[LastName])), N''), N'Guest'), 200),
    {Text("j.[email]", 320)},
    t.[Tier], NULL, NULL, st.[Status], N'Moved',
    COALESCE({Utc("j.[rsvpAtUtc]")}, d.[UpdatedAtUtc]),
    NULL,
    CASE WHEN st.[Status] = N'CheckedIn' THEN COALESCE({Utc("j.[checkedInAtUtc]")}, d.[UpdatedAtUtc]) END,
    NULL, NULL, NULL,
    CASE WHEN st.[Status] = N'Cancelled' THEN d.[UpdatedAtUtc] END,
    NULL, NULL, NULL,
    CASE WHEN t.[Tier] = N'Guest' THEN ev.[GuestPrice] ELSE ev.[MemberPrice] END, ev.[Currency],
    NULL, NULL, NULL, NULL, NULL, NULL, NULL,
    {Text("j.[importedFromId]", 100)},
    d.[UpdatedAtUtc], NULL, NULL, d.[UpdatedAtUtc]
FROM [ClubDocuments] d
{JsonObject}
CROSS APPLY OPENJSON(p.[Json]) WITH (
    [eventId] nvarchar(max) '$.eventId', [memberId] nvarchar(max) '$.memberId', [memberName] nvarchar(max) '$.memberName',
    [email] nvarchar(max) '$.email', [ticketTier] nvarchar(max) '$.ticketTier', [status] nvarchar(max) '$.status',
    [rsvpAtUtc] nvarchar(max) '$.rsvpAtUtc', [checkedInAtUtc] nvarchar(max) '$.checkedInAtUtc',
    [importedFromId] nvarchar(max) '$.importedFromId') j
JOIN [ClubEvents] ev ON ev.[Id] = TRY_CONVERT(uniqueidentifier, j.[eventId]) AND ev.[OrganizationId] = d.[OrganizationId]
LEFT JOIN [ClubMembers] m ON m.[Id] = TRY_CONVERT(uniqueidentifier, j.[memberId]) AND m.[OrganizationId] = d.[OrganizationId]
CROSS APPLY (SELECT CASE WHEN j.[ticketTier] = N'Guest' THEN N'Guest' ELSE N'Member' END AS [Tier]) t
CROSS APPLY (SELECT CASE WHEN j.[status] IN (N'Reserved', N'CheckedIn', N'Waitlisted', N'Cancelled') THEN j.[status] ELSE N'Reserved' END AS [Status]) st
WHERE d.[Kind] = N'event-attendees' AND d.[OrganizationId] IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM [ClubEventRegistrations] x WHERE x.[Id] = d.[Id]);

-- ── what can be booked ───────────────────────────────────────────────────────
INSERT INTO [ClubResources] ([Id], [OrganizationId], [Name], [Kind], [Capacity], [Description], [BookingMode], [DepositAmount],
    [NoShowFee], [Currency], [Active], [SortOrder], [CreatedAtUtc], [CreatedByUserId], [UpdatedAtUtc], [BookingsChangedAtUtc],
    [EditVersion], [Version])
SELECT d.[Id], d.[OrganizationId],
    LEFT(COALESCE({Text("j.[name]", 200)}, N'Resource'), 200),
    k.[Kind],
    CASE WHEN TRY_CAST(j.[capacity] AS int) > 0 THEN TRY_CAST(j.[capacity] AS int) ELSE 1 END,
    {Text("j.[description]", 1000)},
    CASE WHEN k.[Kind] IN (N'Dining', N'Lounge', N'Studio') THEN N'Shared' ELSE N'Exclusive' END,
    0, 0, {Currency}, 1, 0, d.[UpdatedAtUtc], NULL, d.[UpdatedAtUtc], NULL, NEWID(), NEWID()
FROM [ClubDocuments] d
{JsonObject}
CROSS APPLY OPENJSON(p.[Json]) WITH (
    [name] nvarchar(max) '$.name', [kind] nvarchar(max) '$.kind', [capacity] nvarchar(max) '$.capacity',
    [description] nvarchar(max) '$.description') j
LEFT JOIN [Organizations] o ON o.[Id] = d.[OrganizationId]
CROSS APPLY (SELECT CASE WHEN j.[kind] IN (N'Dining', N'Meeting', N'Spa', N'Court', N'Studio', N'Lounge') THEN j.[kind] ELSE N'Meeting' END AS [Kind]) k
WHERE d.[Kind] = N'resources' AND d.[OrganizationId] IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM [ClubResources] x WHERE x.[Id] = d.[Id]);

-- ── bookings ─────────────────────────────────────────────────────────────────
INSERT INTO [ClubReservations] ([Id], [OrganizationId], [ResourceId], [ResourceName], [MemberId], [MemberName], [MemberEmail],
    [PartySize], [StartAtUtc], [EndAtUtc], [Status], [Notes], [DepositAmount], [Currency], [DepositChargeId], [DepositPaymentId],
    [DepositReleasedAtUtc], [DepositWaived], [DepositWaivedReason], [DepositWaivedByName], [NoShowFee], [NoShowChargeId],
    [NoShowFeeReversedAtUtc], [CancelledAtUtc], [CancelledByName], [CreatedAtUtc], [CreatedByUserId], [CreatedByName],
    [UpdatedAtUtc], [Version])
SELECT d.[Id], d.[OrganizationId],
    COALESCE(TRY_CONVERT(uniqueidentifier, j.[resourceId]), @empty),
    LEFT(COALESCE(res.[Name], {Text("j.[resourceName]", 200)}, N'Unknown resource'), 200),
    COALESCE(byId.[Id], byEmail.[Id]),
    LEFT(COALESCE({Text("j.[memberName]", 200)}, NULLIF(LTRIM(RTRIM(byId.[FirstName] + N' ' + byId.[LastName])), N''), N'Guest'), 200),
    COALESCE({Text("j.[memberEmail]", 320)}, NULLIF(byId.[Email], N'')),
    CASE WHEN TRY_CAST(j.[partySize] AS int) > 0 THEN TRY_CAST(j.[partySize] AS int) ELSE 1 END,
    s.[StartAtUtc],
    CASE WHEN {Utc("j.[endAtUtc]")} > s.[StartAtUtc] THEN {Utc("j.[endAtUtc]")} ELSE DATEADD(hour, 1, s.[StartAtUtc]) END,
    CASE WHEN j.[status] IN (N'Pending', N'Confirmed', N'CheckedIn', N'Completed', N'Cancelled', N'NoShow') THEN j.[status] ELSE N'Pending' END,
    {Text("j.[notes]", 2000)},
    0, {Currency}, NULL, NULL, NULL, 0, NULL, NULL, NULL, NULL, NULL,
    CASE WHEN j.[status] = N'Cancelled' THEN d.[UpdatedAtUtc] END, NULL,
    d.[UpdatedAtUtc], NULL, NULL, d.[UpdatedAtUtc], NEWID()
FROM [ClubDocuments] d
{JsonObject}
CROSS APPLY OPENJSON(p.[Json]) WITH (
    [resourceId] nvarchar(max) '$.resourceId', [resourceName] nvarchar(max) '$.resourceName', [memberId] nvarchar(max) '$.memberId',
    [memberName] nvarchar(max) '$.memberName', [memberEmail] nvarchar(max) '$.memberEmail', [partySize] nvarchar(max) '$.partySize',
    [startAtUtc] nvarchar(max) '$.startAtUtc', [endAtUtc] nvarchar(max) '$.endAtUtc', [status] nvarchar(max) '$.status',
    [notes] nvarchar(max) '$.notes') j
LEFT JOIN [Organizations] o ON o.[Id] = d.[OrganizationId]
LEFT JOIN [ClubResources] res ON res.[Id] = TRY_CONVERT(uniqueidentifier, j.[resourceId]) AND res.[OrganizationId] = d.[OrganizationId]
LEFT JOIN [ClubMembers] byId ON byId.[Id] = TRY_CONVERT(uniqueidentifier, j.[memberId]) AND byId.[OrganizationId] = d.[OrganizationId]
OUTER APPLY (
    SELECT TOP 1 m.[Id] FROM [ClubMembers] m
    WHERE m.[OrganizationId] = d.[OrganizationId] AND LEN(LTRIM(RTRIM(j.[memberEmail]))) > 0
      AND LOWER(LTRIM(RTRIM(m.[Email]))) = LOWER(LTRIM(RTRIM(j.[memberEmail])))
      AND (SELECT COUNT(*) FROM [ClubMembers] m2
           WHERE m2.[OrganizationId] = d.[OrganizationId] AND LOWER(LTRIM(RTRIM(m2.[Email]))) = LOWER(LTRIM(RTRIM(j.[memberEmail])))) = 1
) byEmail
CROSS APPLY (SELECT COALESCE({Utc("j.[startAtUtc]")}, d.[UpdatedAtUtc]) AS [StartAtUtc]) s
WHERE d.[Kind] = N'reservations' AND d.[OrganizationId] IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM [ClubReservations] x WHERE x.[Id] = d.[Id]);";
    }
}
