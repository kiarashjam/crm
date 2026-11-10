using System.Text.Json;
using ACI.Infrastructure.Migrations;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ACI.WebApi.Tests;

/// <summary>
/// The copy of the club's event and booking documents into typed tables (the
/// MoveEventsAndReservationsToTables migration), on a real SQL Server, through the migration
/// path production takes.
/// </summary>
/// <remarks>
/// A database is brought to the schema just before the move and filled with the documents a
/// club would have: events with RSVPs taken from the list without names, attendees who are
/// members, guests, checked in, waitlisted and cancelled, one whose member has left and one
/// whose event is gone, the seeded resources, and bookings that name their member only by an
/// email (one address unique, one shared by two members), plus documents with no workspace or
/// no JSON. After the migration every event must keep its seats, every booking its member when
/// one can be told, the documents must all still be there, and a second run must add nothing.
/// </remarks>
public class ClubEventsMoveTests
{
    private const string Password = "correct horse battery staple";

    private sealed class Seed
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public string Email { get; } = $"{Guid.NewGuid():N}@example.ch";
        public Guid Org { get; } = Guid.NewGuid();
        public Guid Jean { get; } = Guid.NewGuid();
        public Guid Marie { get; } = Guid.NewGuid();
        public Guid TwinA { get; } = Guid.NewGuid();
        public Guid TwinB { get; } = Guid.NewGuid();
        public Guid WineNight { get; } = Guid.NewGuid();
        public Guid Gala { get; } = Guid.NewGuid();
        public Guid Spa { get; } = Guid.NewGuid();
        public Guid Dining { get; } = Guid.NewGuid();
        public Guid Court { get; } = Guid.NewGuid();
        public Guid BookedByEmail { get; } = Guid.NewGuid();
        public Guid BookedByTwinEmail { get; } = Guid.NewGuid();
        public Guid BookedById { get; } = Guid.NewGuid();
        public Guid BookedForGoneResource { get; } = Guid.NewGuid();
        public List<(Guid Id, Guid? Org, string Kind, string Payload)> Documents { get; } = new();

        public Guid Doc(Guid? org, string kind, object payload, Guid? id = null)
        {
            var docId = id ?? Guid.NewGuid();
            Documents.Add((docId, org, kind, JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web))));
            return docId;
        }
    }

    private static string Iso(DateTime value) => value.ToUniversalTime().ToString("o");

    private static Seed Club()
    {
        var s = new Seed();
        var soon = DateTime.UtcNow.AddDays(7).Date.AddHours(18);

        // Wine night: capacity 10, 6 seats counted, of which 3 have names (two reserved, one
        // checked in), so 3 were RSVPs from the list. The waitlist counts 2, one named.
        s.Doc(s.Org, "events", new
        {
            name = "Wine night", description = "Loire wines", location = "Library Bar", startAtUtc = Iso(soon), endAtUtc = Iso(soon.AddHours(3)),
            capacity = 10, registeredCount = 6, waitlistCount = 2, memberPrice = 35, guestPrice = 55.5, visibility = "MembersAndGuests",
            status = "Published", coverEmoji = "🍷",
        }, s.WineNight);
        // A gala with no limit, a missing end and a price written as text.
        s.Doc(s.Org, "events", new
        {
            name = "Gala", startAtUtc = Iso(soon.AddDays(20)), capacity = 0, registeredCount = 1, waitlistCount = 0,
            memberPrice = "120", guestPrice = 0, visibility = "Nonsense", status = "Draft",
        }, s.Gala);
        void Attendee(Guid eventId, string name, string tier, string status, Guid? member = null, string? email = null) =>
            s.Doc(s.Org, "event-attendees", new
            {
                eventId, memberId = member, memberName = name, email, ticketTier = tier, status, rsvpAtUtc = Iso(soon.AddDays(-5)),
                checkedInAtUtc = status == "CheckedIn" ? Iso(soon.AddMinutes(5)) : null,
            });
        Attendee(s.WineNight, "Jean Dupont", "Member", "Reserved", s.Jean, "jean@example.ch");
        Attendee(s.WineNight, "Claire Martin", "Guest", "Reserved", email: "claire@example.ch");
        Attendee(s.WineNight, "Marie Dupont", "Member", "CheckedIn", s.Marie);
        Attendee(s.WineNight, "Paul Left", "Member", "Waitlisted", Guid.NewGuid());
        Attendee(s.WineNight, "Ana Cancelled", "Guest", "Cancelled");
        Attendee(s.Gala, "Jean Dupont", "Member", "Reserved", s.Jean);
        Attendee(Guid.NewGuid(), "Of a deleted event", "Guest", "Reserved");

        s.Doc(s.Org, "resources", new { name = "Spa Suite", kind = "Spa", capacity = 1 }, s.Spa);
        s.Doc(s.Org, "resources", new { name = "Main Dining Room", kind = "Dining", capacity = 40, description = "Open kitchen" }, s.Dining);
        s.Doc(s.Org, "resources", new { name = "Tennis Court", kind = "Tennis", capacity = 0 }, s.Court);

        s.Doc(s.Org, "reservations", new
        {
            resourceId = s.Spa, resourceName = "Spa Suite", memberName = "Jean Dupont", memberEmail = " Jean@Example.ch ", partySize = 1,
            startAtUtc = Iso(soon.AddDays(1)), endAtUtc = Iso(soon.AddDays(1).AddHours(1)), status = "Confirmed", notes = "Deep tissue",
        }, s.BookedByEmail);
        s.Doc(s.Org, "reservations", new
        {
            resourceId = s.Dining, resourceName = "Main Dining Room", memberName = "One of the twins", memberEmail = "twins@example.ch", partySize = 4,
            startAtUtc = Iso(soon.AddDays(2)), endAtUtc = Iso(soon.AddDays(2).AddHours(2)), status = "Pending",
        }, s.BookedByTwinEmail);
        s.Doc(s.Org, "reservations", new
        {
            resourceId = s.Dining, memberId = s.Marie, memberName = "Marie Dupont", partySize = 2,
            startAtUtc = Iso(soon.AddDays(3)), endAtUtc = Iso(soon.AddDays(3)), status = "Weird",
        }, s.BookedById);
        s.Doc(s.Org, "reservations", new
        {
            resourceId = Guid.NewGuid(), resourceName = "The old boathouse", memberName = "Walk-in party", partySize = 6,
            startAtUtc = Iso(soon.AddDays(4)), endAtUtc = Iso(soon.AddDays(4).AddHours(2)), status = "Cancelled",
        }, s.BookedForGoneResource);

        // Left behind: the personal bucket's event, and a document that is not JSON.
        s.Doc(null, "events", new { name = "Personal", startAtUtc = Iso(soon), endAtUtc = Iso(soon.AddHours(1)), capacity = 5 });
        s.Documents.Add((Guid.NewGuid(), s.Org, "reservations", "not json at all"));
        return s;
    }

    private static async Task SeedAsync(string database, Seed s)
    {
        var hash = new ACI.Infrastructure.Services.BcryptPasswordHasher().Hash(Password);
        var sql = new List<string>
        {
            $@"INSERT INTO [Users] ([Id], [Name], [Email], [PasswordHash], [TwoFactorEnabled], [CreatedAtUtc], [EmailVerifiedAtUtc])
               VALUES ('{s.UserId}', N'Events manager', N'{s.Email}', N'{hash}', 0, SYSUTCDATETIME(), SYSUTCDATETIME());",
            $@"INSERT INTO [Organizations] ([Id], [Name], [OwnerUserId], [CreatedAtUtc], [StaleDealDays], [Currency]) VALUES ('{s.Org}', N'Pavillon46', '{s.UserId}', SYSUTCDATETIME(), 30, N'CHF');",
            $@"INSERT INTO [OrganizationMembers] ([OrganizationId], [UserId], [Role], [JoinedAtUtc]) VALUES ('{s.Org}', '{s.UserId}', N'Owner', SYSUTCDATETIME());",
        };
        foreach (var (id, first, email) in new[] { (s.Jean, "Jean", "jean@example.ch"), (s.Marie, "Marie", "marie@example.ch"), (s.TwinA, "Alex", "twins@example.ch"), (s.TwinB, "Sam", "twins@example.ch") })
            sql.Add($@"INSERT INTO [ClubMembers] ([Id], [OrganizationId], [OwnerUserId], [FirstName], [LastName], [Email], [Tier], [Status],
                [JoinedAtUtc], [RenewsAtUtc], [DuesAmount], [DuesFrequency], [HouseAccountBalance], [ManualBalance])
                VALUES ('{id}', '{s.Org}', '{s.UserId}', N'{first}', N'Dupont', N'{email}', N'Gold', N'Active', '2024-01-15', '2027-01-15', 0, N'Monthly', 0, 0);");
        foreach (var (id, org, kind, payload) in s.Documents)
            sql.Add($@"INSERT INTO [ClubDocuments] ([Id], [OrganizationId], [Kind], [Payload], [UpdatedAtUtc], [Version])
                       VALUES ('{id}', {(org == null ? "NULL" : $"'{org}'")}, N'{kind}', N'{payload.Replace("'", "''")}', SYSUTCDATETIME(), NEWID());");
        foreach (var statement in sql)
            await SqlServerApp.ExecuteAsync(database, statement);
    }

    private static readonly string[] Tables = ["ClubEvents", "ClubEventRegistrations", "ClubResources", "ClubReservations"];

    private static async Task<Dictionary<string, int>> CountsAsync(string database)
    {
        var counts = new Dictionary<string, int>();
        foreach (var table in Tables)
            counts[table] = (int)(await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [{table}]"))!;
        return counts;
    }

    private static Task<object?> Value(string database, string sql) => SqlServerApp.ScalarAsync(database, sql);

    [SqlServerFact]
    public async Task EveryEventKeepsItsSeatsAndEveryBookingItsMember_AndASecondRunAddsNothing()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            var migrations = SqlServerSchema.AllMigrationIds();
            var move = migrations.Single(m => m.EndsWith("_MoveEventsAndReservationsToTables", StringComparison.Ordinal));
            await using (var db = SqlServerSchema.Context(database))
                await db.GetService<IMigrator>().MigrateAsync(migrations[migrations.ToList().IndexOf(move) - 1]);
            var club = Club();
            await SeedAsync(database, club);
            var documents = (int)(await Value(database, "SELECT COUNT(*) FROM [ClubDocuments]"))!;

            // The migration production runs at start-up, after the ones before it.
            await SqlServerSchema.MigrateAloneAsync(database);

            (await CountsAsync(database)).Should().Equal(new Dictionary<string, int>
            {
                ["ClubEvents"] = 2, ["ClubEventRegistrations"] = 6, ["ClubResources"] = 3, ["ClubReservations"] = 4,
            }, "the personal bucket's event, the attendee of a deleted event and the broken document stay behind");
            (await Value(database, "SELECT COUNT(*) FROM [ClubDocuments]")).Should().Be(documents, "the move copies; the documents stay");

            // The seats the list's RSVPs took without a name still count.
            (await Value(database, $"SELECT [UnnamedSeats] FROM [ClubEvents] WHERE [Id] = '{club.WineNight}'")).Should().Be(3);
            (await Value(database, $"SELECT [UnnamedWaitlist] FROM [ClubEvents] WHERE [Id] = '{club.WineNight}'")).Should().Be(1);
            (await Value(database, $"SELECT [GuestPrice] FROM [ClubEvents] WHERE [Id] = '{club.WineNight}'")).Should().Be(55.5m);
            (await Value(database, $"SELECT [Currency] FROM [ClubEvents] WHERE [Id] = '{club.WineNight}'")).Should().Be("CHF");
            (await Value(database, $"SELECT [MemberPrice] FROM [ClubEvents] WHERE [Id] = '{club.Gala}'")).Should().Be(120m);
            (await Value(database, $"SELECT [Visibility] FROM [ClubEvents] WHERE [Id] = '{club.Gala}'")).Should().Be("MembersAndGuests");
            (await Value(database, $"SELECT DATEDIFF(hour, [StartAtUtc], [EndAtUtc]) FROM [ClubEvents] WHERE [Id] = '{club.Gala}'")).Should().Be(2,
                "an event with no end is given two hours rather than ending before it starts");
            (await Value(database, $"SELECT [UnnamedSeats] FROM [ClubEvents] WHERE [Id] = '{club.Gala}'")).Should().Be(0);

            // Attendees come over as they were, charged nothing; a member who left is a name.
            (await Value(database, "SELECT COUNT(*) FROM [ClubEventRegistrations] WHERE [Source] = N'Moved' AND [ChargeId] IS NULL")).Should().Be(6);
            (await Value(database, "SELECT COUNT(*) FROM [ClubEventRegistrations] WHERE [Status] = N'CheckedIn' AND [CheckedInAtUtc] IS NOT NULL")).Should().Be(1);
            (await Value(database, "SELECT COUNT(*) FROM [ClubEventRegistrations] WHERE [Name] = N'Paul Left' AND [MemberId] IS NULL AND [Status] = N'Waitlisted'")).Should().Be(1);
            (await Value(database, $"SELECT COUNT(*) FROM [ClubEventRegistrations] WHERE [MemberId] = '{club.Jean}'")).Should().Be(2);

            // Resources get the mode their kind has; one with an unknown kind is booked alone.
            (await Value(database, $"SELECT [BookingMode] FROM [ClubResources] WHERE [Id] = '{club.Dining}'")).Should().Be("Shared");
            (await Value(database, $"SELECT [BookingMode] FROM [ClubResources] WHERE [Id] = '{club.Spa}'")).Should().Be("Exclusive");
            (await Value(database, $"SELECT [Kind] + N' ' + CONVERT(nvarchar(10), [Capacity]) FROM [ClubResources] WHERE [Id] = '{club.Court}'")).Should().Be("Meeting 1");

            // A booking finds its member by id, or by an email only one member has.
            (await Value(database, $"SELECT [MemberId] FROM [ClubReservations] WHERE [Id] = '{club.BookedByEmail}'")).Should().Be(club.Jean);
            (await Value(database, $"SELECT [MemberId] FROM [ClubReservations] WHERE [Id] = '{club.BookedByTwinEmail}'")).Should().BeNull(
                "two members share that address, so the booking stays under its name");
            (await Value(database, $"SELECT [MemberId] FROM [ClubReservations] WHERE [Id] = '{club.BookedById}'")).Should().Be(club.Marie);
            (await Value(database, $"SELECT [Status] FROM [ClubReservations] WHERE [Id] = '{club.BookedById}'")).Should().Be("Pending");
            (await Value(database, $"SELECT [ResourceName] FROM [ClubReservations] WHERE [Id] = '{club.BookedForGoneResource}'")).Should().Be("The old boathouse");

            // A second run, as an idempotent migration script would make, adds nothing.
            var counts = await CountsAsync(database);
            // As the script runs it: not through EF's raw SQL, which reads the braces in it as placeholders.
            await SqlServerApp.ExecuteAsync(database, MoveEventsAndReservationsToTables.MoveDocumentsSql);
            (await CountsAsync(database)).Should().Equal(counts);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
