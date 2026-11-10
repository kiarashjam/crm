using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ACI.Application.Services;
using ACI.Infrastructure.Migrations;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Applications and households on a real SQL Server: approvals at the same moment on one
/// row, the household's filtered unique indexes (one primary, one household per person), and
/// the AddMembershipBridgeAndHouseholds migration copying the documents and couples over.
/// </summary>
/// <remarks>
/// The in-memory provider the HTTP tests run on has no transactions or locks, so there the
/// approval's lease and version decide a race; here the claim is an UPDATE in a transaction,
/// and the second of two approvals waits for the first and finds it approved. The gate holds
/// every approval until all of them have read the open application, every time.
/// </remarks>
public class ClubMembershipOnSqlServerTests
{
    private const string Password = "correct horse battery staple";

    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static Task<object?> Value(string database, string sql) => SqlServerApp.ScalarAsync(database, sql);

    /// <summary>A club on its own database, and the gate the approval reports its read to.</summary>
    private static async Task RunAsync(Func<HttpClient, RaceFactory.ReadGate, Guid, string, Task> test)
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            var gate = new RaceFactory.ReadGate();
            using var app = SqlServerApp.Start(database, services: services => services.AddSingleton<ILedgerReadObserver>(gate));
            var (client, _) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
            var org = (await Json(await client.PostAsync("/api/organizations", Body(new { name = "Pavillon46" })), "create the club")).GetProperty("id").GetGuid();
            client.In(org);
            await test(client, gate, org, database);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    private static async Task<Guid> ApplyAsync(HttpClient client, string first, string last, object? extra = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["firstName"] = first, ["lastName"] = last, ["email"] = $"{first.ToLowerInvariant()}@example.ch", ["requestedTier"] = "Gold",
        };
        if (extra != null)
            foreach (var p in extra.GetType().GetProperties()) body[p.Name] = p.GetValue(extra);
        return (await Json(await client.PostAsync("/api/applications", Body(body)), $"{first} applies")).GetProperty("id").GetGuid();
    }

    [SqlServerFact]
    public Task FourReviewersApprovingAtOnce_MakeOneMemberWithOneFeeOneSubscriptionAndOneWelcome() => RunAsync(async (client, gate, org, database) =>
    {
        var id = await ApplyAsync(client, "Claire", "Martin");
        gate.Arm("applications", readers: 4);

        var answers = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.PostAsync($"/api/applications/{id}/approve", Body(new { }))));

        var results = new List<JsonElement>();
        foreach (var answer in answers) results.Add(await Json(answer, "approve at once"));
        var member = results[0].GetProperty("memberId").GetGuid();
        results.Select(r => r.GetProperty("memberId").GetGuid()).Should().AllBeEquivalentTo(member, "every reviewer is answered with the one member");
        results.Count(r => r.GetProperty("approved").GetBoolean()).Should().Be(1, "one approval did it; the others found it done");

        (await Value(database, $"SELECT COUNT(*) FROM [ClubMembers] WHERE [OrganizationId] = '{org}'")).Should().Be(1);
        (await Value(database, $"SELECT COUNT(*) FROM [ClubHouseAccounts] WHERE [MemberId] = '{member}'")).Should().Be(1);
        (await Value(database, $"SELECT COUNT(*) FROM [ClubCharges] WHERE [OrganizationId] = '{org}' AND [IdempotencyKey] = N'initiation:{member}'")).Should().Be(1);
        (await Value(database, $"SELECT COUNT(*) FROM [ClubCharges] WHERE [OrganizationId] = '{org}'")).Should().Be(1, "one fee, and no second of anything");
        (await Value(database, $"SELECT COUNT(*) FROM [ClubSubscriptions] WHERE [MemberId] = '{member}'")).Should().Be(1);
        (await Value(database, $"SELECT COUNT(*) FROM [OutboundMessages] WHERE [OrganizationId] = '{org}' AND [Source] = N'welcome'")).Should().Be(1);
        (await Value(database, $"SELECT [Status] FROM [ClubApplications] WHERE [Id] = '{id}'")).Should().Be("Approved");
        (await Value(database, $"SELECT COUNT(*) FROM [ClubApplications] WHERE [Id] = '{id}' AND [ApprovalClaimedUntilUtc] IS NULL AND [MemberId] = '{member}'"))
            .Should().Be(1, "the claim is let go once the approval commits");
    });

    [SqlServerFact]
    public Task AnApprovalJoiningAHousehold_ThenANewPrimary_ThenAMerge_KeepOnePrimaryAndOneHouseholdEach() => RunAsync(async (client, _, org, database) =>
    {
        async Task<Guid> Member(string first) => (await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = "Meier", email = $"{first.ToLowerInvariant()}.meier@example.ch", tier = "Gold", status = "Active",
        })), $"add {first}")).GetProperty("id").GetGuid();
        var peter = await Member("Peter");
        var household = (await Json(await client.PostAsync("/api/households", Body(new { name = "Meier", primaryMemberId = peter })), "household"))
            .GetProperty("id").GetGuid();

        // Anna applies as Peter's spouse, her bills on his account.
        var application = await ApplyAsync(client, "Anna", "Meier", new { householdId = household, householdRole = "Spouse" });
        var approved = await Json(await client.PostAsync($"/api/applications/{application}/approve", Body(new { rollUpBilling = true })), "approve Anna");
        var anna = approved.GetProperty("memberId").GetGuid();
        approved.GetProperty("joinedHousehold").GetBoolean().Should().BeTrue();
        (await Value(database, $"SELECT [MemberId] FROM [ClubCharges] WHERE [IdempotencyKey] = N'initiation:{anna}'")).Should().Be(peter,
            "her fee rolls up to the primary");
        (await Value(database, $"SELECT [IncurredByMemberId] FROM [ClubCharges] WHERE [IdempotencyKey] = N'initiation:{anna}'")).Should().Be(anna);

        // Anna becomes the primary: the old primary steps down in the same transaction, so the index never sees two.
        var state = await Json(await client.GetAsync($"/api/households/{household}"), "read the household");
        var annaLink = state.GetProperty("people").EnumerateArray().Single(p => p.GetProperty("memberId").GetGuid() == anna).GetProperty("linkId").GetGuid();
        await Json(await client.PostAsync($"/api/households/{household}/primary", Body(new { linkId = annaLink })), "Anna becomes primary");
        (await Value(database, $"SELECT COUNT(*) FROM [ClubHouseholdMembers] WHERE [HouseholdId] = '{household}' AND [IsPrimary] = 1")).Should().Be(1);
        (await Value(database, $"SELECT [Role] FROM [ClubHouseholdMembers] WHERE [MemberId] = '{peter}'")).Should().Be("Spouse");

        // A duplicate of Anna heads a household of her own with a child in it. Merged into Anna,
        // she stays in Meier, and the household she headed passes to the child.
        var duplicate = await Member("Annie");
        var kit = await Member("Kit");
        var old = (await Json(await client.PostAsync("/api/households", Body(new { name = "Old Meier", primaryMemberId = duplicate })), "the old household"))
            .GetProperty("id").GetGuid();
        await Json(await client.PostAsync($"/api/households/{old}/members", Body(new { memberId = kit, role = "Child" })), "Kit joins it");
        (await client.PostAsync($"/api/members/{anna}/merge", Body(new { otherMemberId = duplicate }))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await Value(database, $"SELECT COUNT(*) FROM [ClubHouseholdMembers] WHERE [MemberId] = '{anna}'")).Should().Be(1);
        (await Value(database, $"SELECT COUNT(*) FROM [ClubHouseholdMembers] WHERE [MemberId] = '{duplicate}'")).Should().Be(0);
        (await Value(database, $"SELECT CONVERT(nvarchar(36), [MemberId]) + N' ' + [Role] FROM [ClubHouseholdMembers] WHERE [HouseholdId] = '{old}' AND [IsPrimary] = 1"))
            .Should().Be($"{kit.ToString().ToUpperInvariant()} Primary");
        (await Value(database, $"SELECT COUNT(*) FROM [ClubHouseholdMembers] WHERE [OrganizationId] = '{org}' AND [IsPrimary] = 1")).Should().Be(2,
            "one primary in each household");
    });

    // ── the migration ────────────────────────────────────────────────────────

    private sealed class Seed
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public string Email { get; } = $"{Guid.NewGuid():N}@example.ch";
        public Guid Org { get; } = Guid.NewGuid();

        public Guid LeadJean { get; } = Guid.NewGuid();
        public Guid LeadLena { get; } = Guid.NewGuid();

        public Guid ContactJean { get; } = Guid.NewGuid();
        public Guid ContactEloise { get; } = Guid.NewGuid();
        public Guid ContactAlex { get; } = Guid.NewGuid();
        public Guid ContactPaula { get; } = Guid.NewGuid();
        public Guid ContactNinaArchived { get; } = Guid.NewGuid();
        public Guid ContactLena { get; } = Guid.NewGuid();
        public Guid ContactClaire { get; } = Guid.NewGuid();
        public Guid ContactMarc { get; } = Guid.NewGuid();
        public Guid ContactTom { get; } = Guid.NewGuid();
        public Guid ContactRobin { get; } = Guid.NewGuid();
        public Guid ContactGone { get; } = Guid.NewGuid();
        public Guid ContactFritz { get; } = Guid.NewGuid();

        public Guid Jean { get; } = Guid.NewGuid();
        public Guid Eloise { get; } = Guid.NewGuid();
        public Guid Alex { get; } = Guid.NewGuid();
        public Guid Sam { get; } = Guid.NewGuid();
        public Guid Paul { get; } = Guid.NewGuid();
        public Guid Nina { get; } = Guid.NewGuid();
        public Guid Kit { get; } = Guid.NewGuid();

        public Guid AppLena { get; } = Guid.NewGuid();
        public Guid AppJean { get; } = Guid.NewGuid();
        public Guid AppOdd { get; } = Guid.NewGuid();
        public Guid AppSam { get; } = Guid.NewGuid();

        public Guid Dupont { get; } = Guid.NewGuid();
        public Guid OldDupont { get; } = Guid.NewGuid();
        public Guid Weber { get; } = Guid.NewGuid();
        public Guid Twins { get; } = Guid.NewGuid();
        public Guid Favre { get; } = Guid.NewGuid();

        public List<(Guid Id, Guid? Org, string Kind, string Payload, DateTime At)> Documents { get; } = new();

        public void Doc(Guid? org, string kind, object payload, Guid? id = null, DateTime? at = null) =>
            Documents.Add((id ?? Guid.NewGuid(), org, kind, JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                at ?? new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc)));
    }

    private static string Sql(DateTime at) => at.ToString("yyyy-MM-dd HH:mm:ss");

    private static Seed Club()
    {
        var s = new Seed();
        // Applications as the old page wrote them.
        s.Doc(s.Org, "applications", new
        {
            firstName = "Lena", lastName = "Keller", email = "lena@example.ch", requestedTier = "Gold", status = "Submitted",
            submittedAtUtc = "2024-09-01T10:00:00Z", reasonForJoining = "The chef series",
        }, s.AppLena);
        s.Doc(s.Org, "applications", new
        {
            firstName = "Jean", lastName = "Dupont", email = "jean@example.ch", requestedTier = "Gold", status = "Approved",
            submittedAtUtc = "2024-01-02T10:00:00Z", reviewedAtUtc = "2024-01-10T09:00:00Z", reviewerNotes = "Welcome",
        }, s.AppJean);
        s.Doc(s.Org, "applications", new { firstName = "  Odd ", lastName = "One", email = "", requestedTier = "Silver", status = "Pending" }, s.AppOdd);
        s.Doc(s.Org, "applications", new
        {
            firstName = "Sam", lastName = "Dupont", email = "dupont.family@example.ch", requestedTier = "Gold", status = "Approved",
            submittedAtUtc = "2024-02-01T10:00:00Z",
        }, s.AppSam);
        s.Doc(null, "applications", new { firstName = "Personal", lastName = "Bucket", email = "p@example.ch", status = "Submitted" });
        s.Documents.Add((Guid.NewGuid(), s.Org, "applications", "not json at all", DateTime.UtcNow));

        // Households, and who the documents put in them.
        s.Doc(s.Org, "households", new { name = "Dupont", primaryMemberId = s.Alex, createdAtUtc = "2024-01-01T00:00:00Z", notes = "Gate code 1234" }, s.Dupont);
        s.Doc(s.Org, "households", new { name = "Old Dupont", createdAtUtc = "2024-06-01T00:00:00Z" }, s.OldDupont);
        s.Doc(s.Org, "households", new { name = "Weber", primaryMemberId = s.Paul, createdAtUtc = "2024-02-01T00:00:00Z" }, s.Weber);
        s.Doc(s.Org, "households", new { name = "Twins", createdAtUtc = "2024-03-01T00:00:00Z" }, s.Twins);
        s.Doc(s.Org, "households", new { name = "Favre", primaryMemberId = s.Eloise, createdAtUtc = "2024-04-01T00:00:00Z" }, s.Favre);
        void Link(Guid household, Guid member, string role, DateTime at) =>
            s.Doc(s.Org, "household-members", new { householdId = household, memberId = member, role }, at: at);
        Link(s.Dupont, s.Alex, "Primary", new DateTime(2024, 1, 1, 13, 0, 0, DateTimeKind.Utc));
        Link(s.Dupont, s.Sam, "Spouse", new DateTime(2024, 1, 1, 14, 0, 0, DateTimeKind.Utc));
        Link(s.Dupont, s.Jean, "Dependent", new DateTime(2024, 1, 1, 15, 0, 0, DateTimeKind.Utc));
        Link(s.OldDupont, s.Sam, "Other", new DateTime(2024, 6, 1, 13, 0, 0, DateTimeKind.Utc));
        Link(s.Twins, s.Nina, "Primary", new DateTime(2024, 3, 2, 0, 0, 0, DateTimeKind.Utc));
        Link(s.Twins, s.Kit, "Primary", new DateTime(2024, 3, 3, 0, 0, 0, DateTimeKind.Utc));
        Link(s.Twins, Guid.NewGuid(), "Child", new DateTime(2024, 3, 4, 0, 0, 0, DateTimeKind.Utc));
        Link(Guid.NewGuid(), s.Kit, "Child", new DateTime(2024, 3, 5, 0, 0, 0, DateTimeKind.Utc));
        return s;
    }

    private static async Task SeedAsync(string database, Seed s)
    {
        var hash = new ACI.Infrastructure.Services.BcryptPasswordHasher().Hash(Password);
        var sql = new List<string>
        {
            $@"INSERT INTO [Users] ([Id], [Name], [Email], [PasswordHash], [TwoFactorEnabled], [CreatedAtUtc], [EmailVerifiedAtUtc])
               VALUES ('{s.UserId}', N'Membership director', N'{s.Email}', N'{hash}', 0, SYSUTCDATETIME(), SYSUTCDATETIME());",
            $@"INSERT INTO [Organizations] ([Id], [Name], [OwnerUserId], [CreatedAtUtc], [StaleDealDays], [Currency]) VALUES ('{s.Org}', N'Pavillon46', '{s.UserId}', SYSUTCDATETIME(), 30, N'CHF');",
            $@"INSERT INTO [OrganizationMembers] ([OrganizationId], [UserId], [Role], [JoinedAtUtc]) VALUES ('{s.Org}', '{s.UserId}', N'Owner', SYSUTCDATETIME());",
        };
        foreach (var (id, name, email, contact) in new[] { (s.LeadJean, "Jean Dupont", "jean@example.ch", s.ContactJean), (s.LeadLena, "Lena Keller", "lena@example.ch", s.ContactLena) })
            sql.Add($@"INSERT INTO [Leads] ([Id], [UserId], [OrganizationId], [Name], [Email], [Status], [IsConverted], [ConvertedToContactId], [CreatedAtUtc],
                [EmailOpenCount], [ScoreIsManual], [SlaBreached], [Version])
                VALUES ('{id}', '{s.UserId}', '{s.Org}', N'{name}', N'{email}', N'Converted', 1, '{contact}', '2023-12-01', 0, 0, 0, NEWID());");
        foreach (var (id, name, email, archived, fromLead) in new (Guid, string, string, bool, Guid?)[]
        {
            (s.ContactJean, "Jean Dupont", "jean@example.ch", false, s.LeadJean),
            (s.ContactEloise, "Eloise FAVRE", "eloise@example.ch", false, null),
            (s.ContactAlex, "Alex Dupont", "dupont.family@example.ch", false, null),
            (s.ContactPaula, "Paula Weber", "paul@example.ch", false, null),
            (s.ContactNinaArchived, "Nina Roth", "nina@example.ch", true, null),
            (s.ContactLena, "Lena Keller", "lena@example.ch", false, s.LeadLena),
            (s.ContactClaire, "Claire Martin", "claire@example.ch", false, null),
            (s.ContactMarc, "Marc Martin", "marc@example.ch", false, null),
            (s.ContactTom, "Tom Favre", "tom@example.ch", false, null),
            (s.ContactRobin, "Robin Meyer", "robin@example.ch", false, null),
            (s.ContactGone, "Gone Person", "gone@example.ch", true, null),
            (s.ContactFritz, "Fritz Friend", "fritz@example.ch", false, null),
        })
            sql.Add($@"INSERT INTO [Contacts] ([Id], [UserId], [OrganizationId], [Name], [Email], [CreatedAtUtc], [IsArchived], [DoNotContact], [ConvertedFromLeadId], [Version])
                VALUES ('{id}', '{s.UserId}', '{s.Org}', N'{name}', N'{email}', '2023-12-01', {(archived ? 1 : 0)}, 0, {(fromLead is { } l ? $"'{l}'" : "NULL")}, NEWID());");
        foreach (var (id, first, last, email, joined) in new[]
        {
            (s.Jean, "Jean", "Dupont", "jean@example.ch", "2024-01-10"),
            (s.Eloise, "Éloïse", "Favre", "eloise@example.ch", "2024-01-11"),
            (s.Alex, "Alex", "Dupont", "dupont.family@example.ch", "2023-05-01"),
            (s.Sam, "Sam", "Dupont", "dupont.family@example.ch", "2023-05-02"),
            (s.Paul, "Paul", "Weber", "paul@example.ch", "2023-06-01"),
            (s.Nina, "Nina", "Roth", "nina@example.ch", "2023-07-01"),
            (s.Kit, "Kit", "Roth", "kit@example.ch", "2023-07-02"),
        })
            sql.Add($@"INSERT INTO [ClubMembers] ([Id], [OrganizationId], [OwnerUserId], [FirstName], [LastName], [Email], [Tier], [Status],
                [JoinedAtUtc], [RenewsAtUtc], [DuesAmount], [DuesFrequency], [HouseAccountBalance], [ManualBalance])
                VALUES ('{id}', '{s.Org}', '{s.UserId}', N'{first}', N'{last}', N'{email}', N'Gold', N'Active', '{joined}', '2027-01-15', 0, N'Monthly', 0, 0);");
        foreach (var (a, b, type, at) in new[]
        {
            (s.ContactClaire, s.ContactMarc, "spouse", "2024-05-01"),
            (s.ContactAlex, s.ContactRobin, "partner", "2024-05-02"),
            (s.ContactTom, s.ContactEloise, "spouse", "2024-05-03"),
            (s.ContactClaire, s.ContactFritz, "friend", "2024-05-04"),
            (s.ContactMarc, s.ContactGone, "spouse", "2024-05-05"),
            (s.ContactLena, s.ContactJean, "parent", "2024-05-06"),
        })
            sql.Add($@"INSERT INTO [ContactRelationships] ([Id], [OrganizationId], [UserId], [ContactId], [RelatedContactId], [Type], [CreatedAtUtc])
                VALUES (NEWID(), '{s.Org}', '{s.UserId}', '{a}', '{b}', N'{type}', '{at}');");
        foreach (var (id, org, kind, payload, at) in s.Documents)
            sql.Add($@"INSERT INTO [ClubDocuments] ([Id], [OrganizationId], [Kind], [Payload], [UpdatedAtUtc], [Version])
                       VALUES ('{id}', {(org == null ? "NULL" : $"'{org}'")}, N'{kind}', N'{payload.Replace("'", "''")}', '{Sql(at)}', NEWID());");
        foreach (var statement in sql)
            await SqlServerApp.ExecuteAsync(database, statement);
    }

    private static readonly string[] Tables = ["ClubApplications", "ClubHouseholds", "ClubHouseholdMembers"];

    private static async Task<Dictionary<string, int>> CountsAsync(string database)
    {
        var counts = new Dictionary<string, int>();
        foreach (var table in Tables)
            counts[table] = (int)(await Value(database, $"SELECT COUNT(*) FROM [{table}]"))!;
        counts["LinkedMembers"] = (int)(await Value(database, "SELECT COUNT(*) FROM [ClubMembers] WHERE [ContactId] IS NOT NULL"))!;
        return counts;
    }

    private static Task<object?> PersonIn(string database, Guid household, string who) =>
        Value(database, $"SELECT [Role] + CASE WHEN [IsPrimary] = 1 THEN N'*' ELSE N'' END FROM [ClubHouseholdMembers] WHERE [HouseholdId] = '{household}' AND {who}");

    [SqlServerFact]
    public async Task TheMoveBringsApplicationsHouseholdsAndCouplesOver_LinksMembersToTheirContact_AndASecondRunAddsNothing()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            var migrations = SqlServerSchema.AllMigrationIds();
            var move = migrations.Single(m => m.EndsWith("_AddMembershipBridgeAndHouseholds", StringComparison.Ordinal));
            await using (var db = SqlServerSchema.Context(database))
                await db.GetService<IMigrator>().MigrateAsync(migrations[migrations.ToList().IndexOf(move) - 1]);
            var s = Club();
            await SeedAsync(database, s);
            var documents = (int)(await Value(database, "SELECT COUNT(*) FROM [ClubDocuments]"))!;
            var relationships = (int)(await Value(database, "SELECT COUNT(*) FROM [ContactRelationships]"))!;

            // The migration production runs at start-up, after the ones before it.
            await SqlServerSchema.MigrateAloneAsync(database);

            (await CountsAsync(database)).Should().Equal(new Dictionary<string, int>
            {
                ["ClubApplications"] = 4, ["ClubHouseholds"] = 6, ["ClubHouseholdMembers"] = 10, ["LinkedMembers"] = 3,
            }, "the personal bucket's application, the broken document and the links to no one stay behind; one couple makes a household");
            (await Value(database, "SELECT COUNT(*) FROM [ClubDocuments]")).Should().Be(documents, "the move copies; the documents stay");
            (await Value(database, "SELECT COUNT(*) FROM [ContactRelationships]")).Should().Be(relationships, "the relationships stay as they were");

            // Members find their contact: by address and name, accents and case aside, and the lead it came from.
            (await Value(database, $"SELECT [ContactId] FROM [ClubMembers] WHERE [Id] = '{s.Jean}'")).Should().Be(s.ContactJean);
            (await Value(database, $"SELECT [LeadId] FROM [ClubMembers] WHERE [Id] = '{s.Jean}'")).Should().Be(s.LeadJean);
            (await Value(database, $"SELECT [ContactId] FROM [ClubMembers] WHERE [Id] = '{s.Eloise}'")).Should().Be(s.ContactEloise);
            (await Value(database, $"SELECT [ContactId] FROM [ClubMembers] WHERE [Id] = '{s.Alex}'")).Should().Be(s.ContactAlex);
            (await Value(database, $"SELECT [ContactId] FROM [ClubMembers] WHERE [Id] = '{s.Sam}'")).Should().BeNull("Sam shares Alex's mailbox but is someone else");
            (await Value(database, $"SELECT [ContactId] FROM [ClubMembers] WHERE [Id] = '{s.Paul}'")).Should().BeNull("Paula is not Paul");
            (await Value(database, $"SELECT [ContactId] FROM [ClubMembers] WHERE [Id] = '{s.Nina}'")).Should().BeNull("an archived contact is not linked");

            // Applications as they were; the approved ones find the member the old flow made, and post nothing again.
            (await Value(database, $"SELECT CONVERT(nvarchar(36), [ContactId]) + N' ' + CONVERT(nvarchar(36), [LeadId]) + N' ' + [Status] FROM [ClubApplications] WHERE [Id] = '{s.AppLena}'"))
                .Should().Be($"{s.ContactLena.ToString().ToUpperInvariant()} {s.LeadLena.ToString().ToUpperInvariant()} Submitted");
            (await Value(database, $"SELECT [MemberId] FROM [ClubApplications] WHERE [Id] = '{s.AppJean}' AND [ApprovalTrigger] = N'moved' AND [MoneyPending] = 0"))
                .Should().Be(s.Jean);
            (await Value(database, $"SELECT [LeadId] FROM [ClubApplications] WHERE [Id] = '{s.AppJean}'")).Should().Be(s.LeadJean);
            (await Value(database, $"SELECT [MemberId] FROM [ClubApplications] WHERE [Id] = '{s.AppSam}'")).Should().Be(s.Sam);
            (await Value(database, $"SELECT [ContactId] FROM [ClubApplications] WHERE [Id] = '{s.AppSam}'")).Should().BeNull("the contact at that address is Alex's");
            (await Value(database, $"SELECT [FirstName] + N'|' + [Status] + N'|' + [Source] FROM [ClubApplications] WHERE [Id] = '{s.AppOdd}'")).Should().Be("Odd|Submitted|moved");

            // Households: each member once (Sam stays in the first), one primary each, roll-up off.
            (await PersonIn(database, s.Dupont, $"[MemberId] = '{s.Alex}'")).Should().Be("Primary*");
            (await PersonIn(database, s.Dupont, $"[MemberId] = '{s.Sam}'")).Should().Be("Spouse");
            (await PersonIn(database, s.Dupont, $"[MemberId] = '{s.Jean}'")).Should().Be("Dependent");
            (await Value(database, $"SELECT COUNT(*) FROM [ClubHouseholdMembers] WHERE [HouseholdId] = '{s.OldDupont}'")).Should().Be(0);
            (await PersonIn(database, s.Weber, $"[MemberId] = '{s.Paul}'")).Should().Be("Primary*", "the member the household names heads it");
            (await PersonIn(database, s.Twins, $"[MemberId] = '{s.Nina}'")).Should().Be("Primary*", "of two marked Primary, the first");
            (await PersonIn(database, s.Twins, $"[MemberId] = '{s.Kit}'")).Should().Be("Other");
            (await Value(database, "SELECT COUNT(*) FROM [ClubHouseholdMembers] WHERE [RollUpBilling] = 1")).Should().Be(0, "roll-up is left for staff to switch on");
            (await Value(database, $"SELECT [Notes] FROM [ClubHouseholds] WHERE [Id] = '{s.Dupont}'")).Should().Be("Gate code 1234");

            // Couples: Claire and Marc make a household; Tom joins Éloïse's, which had no couple;
            // Alex's household has its couple, so the partner stays a relationship.
            var martin = (Guid)(await Value(database, $"SELECT [HouseholdId] FROM [ClubHouseholdMembers] WHERE [ContactId] = '{s.ContactClaire}'"))!;
            (await Value(database, $"SELECT [Name] FROM [ClubHouseholds] WHERE [Id] = '{martin}'")).Should().Be("Martin");
            (await PersonIn(database, martin, $"[ContactId] = '{s.ContactClaire}'")).Should().Be("Primary*");
            (await PersonIn(database, martin, $"[ContactId] = '{s.ContactMarc}'")).Should().Be("Spouse");
            (await PersonIn(database, s.Favre, $"[ContactId] = '{s.ContactTom}'")).Should().Be("Spouse");
            (await Value(database, $"SELECT COUNT(*) FROM [ClubHouseholdMembers] WHERE [ContactId] = '{s.ContactRobin}' OR [ContactId] = '{s.ContactGone}'")).Should().Be(0);

            // A second run, as an idempotent migration script would make, adds nothing.
            var counts = await CountsAsync(database);
            await SqlServerApp.ExecuteAsync(database, AddMembershipBridgeAndHouseholds.MoveSql);
            (await CountsAsync(database)).Should().Equal(counts);

            // And through the API, as the screens read it once the app starts.
            using var app = SqlServerApp.Start(database);
            var client = app.CreateClient();
            var login = await client.PostAsJsonAsync("/api/auth/login", new { email = s.Email, password = Password });
            var loginBody = await login.Content.ReadAsStringAsync();
            login.IsSuccessStatusCode.Should().BeTrue(loginBody);
            using (var token = JsonDocument.Parse(loginBody))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.RootElement.GetProperty("token").GetString());
            client.DefaultRequestHeaders.Add("X-Organization-Id", s.Org.ToString());

            (await Json(await client.GetAsync("/api/applications"), "applications")).GetArrayLength().Should().Be(4);
            var again = await Json(await client.PostAsync($"/api/applications/{s.AppJean}/approve", Body(new { })), "approve Jean's again");
            (again.GetProperty("memberId").GetGuid(), again.GetProperty("approved").GetBoolean()).Should().Be((s.Jean, false));
            (await Value(database, $"SELECT COUNT(*) FROM [ClubCharges] WHERE [OrganizationId] = '{s.Org}'")).Should().Be(0,
                "a member the old flow admitted is not charged a fee now");

            var households = await Json(await client.GetAsync("/api/households"), "households");
            households.GetArrayLength().Should().Be(6);
            households.EnumerateArray().Single(h => h.GetProperty("id").GetGuid() == martin).GetProperty("people").EnumerateArray()
                .Select(p => (p.GetProperty("name").GetString(), p.GetProperty("role").GetString()))
                .Should().BeEquivalentTo([("Claire Martin", "Primary"), ("Marc Martin", "Spouse")]);
            var claire = await Json(await client.GetAsync($"/api/contacts/{s.ContactClaire}/relationships"), "Claire's relationships");
            claire.GetProperty("items").EnumerateArray().Select(i => (i.GetProperty("relatedContactName").GetString(), i.GetProperty("type").GetString()))
                .Should().BeEquivalentTo([("Marc Martin", "spouse"), ("Fritz Friend", "friend")], "the couple shows once, from the household");
            var links = await Json(await client.GetAsync($"/api/members/{s.Jean}/links"), "Jean's links");
            (links.GetProperty("leadId").GetGuid(), links.GetProperty("contactId").GetGuid()).Should().Be((s.LeadJean, s.ContactJean));
            links.GetProperty("household").GetProperty("name").GetString().Should().Be("Dupont");
            links.GetProperty("applications").EnumerateArray().Single().GetProperty("id").GetGuid().Should().Be(s.AppJean);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
