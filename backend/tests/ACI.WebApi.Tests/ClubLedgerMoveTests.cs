using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ACI.Infrastructure.Migrations;
using ACI.Infrastructure.Services;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ACI.WebApi.Tests;

/// <summary>
/// The move of the club's money documents into the typed ledger (the AddClubMoneyLedger
/// migration), on a real SQL Server, through the migration path production takes.
/// </summary>
/// <remarks>
/// A database is brought to the schema just before the ledger, filled with the documents a
/// club running since before Phase 0 would have (partial payments, voids, a payment that
/// was voided, a charge marked paid with its payment gone, an overpayment the old code
/// dropped, opening balances in credit and owed, tabs open, closed and voided, gift cards
/// redeemed, expired, voided and out of step with their redemptions, codes taken twice,
/// subscriptions, donations, and a copied organization), and then migrated. Every member's
/// balance and every card's value must be what the club saw before, and a second run of
/// the move must add nothing.
/// </remarks>
public class ClubLedgerMoveTests
{
    private const string Password = "correct horse battery staple";

    private sealed class Seed
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public string Email { get; } = $"{Guid.NewGuid():N}@example.ch";
        public Guid OrgA { get; } = Guid.NewGuid();
        public Guid OrgB { get; } = Guid.NewGuid();
        public Guid Jean { get; set; }
        public Guid Marie { get; set; }
        public Guid Paulo { get; set; }
        public Dictionary<Guid, decimal> Balances { get; } = new();
        public Dictionary<Guid, decimal> CardsLeft { get; } = new();
        public Dictionary<Guid, (string Status, decimal Paid)> Charges { get; } = new();
        public List<(Guid Id, Guid? Org, string Kind, string Payload)> Documents { get; } = new();
        public List<string> Sql { get; } = new();

        public Guid Doc(Guid? org, string kind, object payload, Guid? id = null)
        {
            var docId = id ?? Guid.NewGuid();
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Documents.Add((docId, org, kind, json));
            return docId;
        }

        public Guid Member(Guid org, string first, decimal houseBalance, decimal manualBalance, string tier = "Gold")
        {
            var id = Guid.NewGuid();
            Sql.Add($@"INSERT INTO [ClubMembers] ([Id], [OrganizationId], [OwnerUserId], [FirstName], [LastName], [Email], [Tier], [Status],
                [JoinedAtUtc], [RenewsAtUtc], [DuesAmount], [DuesFrequency], [HouseAccountBalance], [ManualBalance])
                VALUES ('{id}', '{org}', '{UserId}', N'{first}', N'Dupont', N'{first.ToLowerInvariant()}@example.ch', N'{tier}', N'Active',
                '2024-01-15T00:00:00', '2027-01-15T00:00:00', 0, N'Monthly', {Num(houseBalance)}, {Num(manualBalance)});");
            Balances[id] = houseBalance;
            return id;
        }
    }

    private static string Num(decimal value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    private static string Iso(DateTime value) => value.ToUniversalTime().ToString("o");

    /// <summary>The club's money as the documents held it before the ledger, and what each balance was.</summary>
    private static Seed Club()
    {
        var s = new Seed();
        var now = DateTime.UtcNow;
        var old = now.AddDays(-40);

        // Jean opened owing 500 (the opening balance fix kept it in ManualBalance), and since:
        // a dinner paid, a bar tab part paid, an event voided, dues paid by a cheque that
        // bounced, a spa visit marked paid whose payment record is gone, and a dinner paid
        // with 100 where the old code kept 90 and dropped the rest.
        var jean = s.Jean = s.Member(s.OrgA, "Jean", houseBalance: -790m, manualBalance: -500m);
        Guid Charge(Guid member, string kind, decimal amount, decimal paid, string status, object? extra = null)
        {
            var id = s.Doc(s.OrgA, "charges", new
            {
                memberId = member, memberName = "Jean Dupont", kind, description = $"{kind} night", amount, paidAmount = paid, status,
                postedAtUtc = Iso(old), dueAtUtc = Iso(old.AddDays(14)), paidAtUtc = paid >= amount ? Iso(old.AddDays(2)) : null,
                voidReason = status == "Voided" ? "Posted twice" : null, voidedByName = status == "Voided" ? "Manager" : null,
                voidedAtUtc = status == "Voided" ? Iso(old.AddDays(1)) : null,
            });
            // Their due date is behind them, so what is still owed is overdue.
            var shown = status == "Voided" ? "Voided" : paid >= amount && amount > 0 ? "Paid" : "Overdue";
            s.Charges[id] = (shown, status == "Voided" ? 0 : paid);
            return id;
        }
        void Payment(Guid member, Guid chargeId, decimal amount, string status = "", string method = "Cash") =>
            s.Doc(s.OrgA, "payments", new
            {
                memberId = member, memberName = "Jean Dupont", amount, method, chargeId, appliedToChargeIds = new[] { chargeId },
                receivedAtUtc = Iso(old.AddDays(2)), status = status.Length == 0 ? null : status,
                voidReason = status == "Voided" ? "Cheque bounced" : null, voidedAtUtc = status == "Voided" ? Iso(old.AddDays(5)) : null,
            });

        var dinner = Charge(jean, "Dining", 100m, 100m, "Paid");
        Payment(jean, dinner, 100m);
        var bar = Charge(jean, "Bar", 150m, 60m, "PartiallyPaid");
        Payment(jean, bar, 60m, method: "CardOnFile");
        Charge(jean, "Event", 80m, 0m, "Voided");
        var dues = Charge(jean, "Dues", 200m, 0m, "Pending");
        Payment(jean, dues, 200m, status: "Voided", method: "Check");
        Charge(jean, "Spa", 120m, 120m, "Paid");
        var overpaid = Charge(jean, "Retail", 90m, 90m, "Paid");
        Payment(jean, overpaid, 100m, method: "Wire");

        // Marie opened 50 in credit; her closed bar tab posted 30, of which she paid 15.
        var marie = s.Marie = s.Member(s.OrgA, "Marie", houseBalance: 35m, manualBalance: 50m, tier: "Silver");
        var tabCharge = Guid.NewGuid();
        var closedTab = s.Doc(s.OrgA, "tabs", new
        {
            memberId = marie, memberName = "Marie Dupont", memberTier = "Silver", venue = "Bar", status = "Closed",
            openedAtUtc = Iso(old), closedAtUtc = Iso(old.AddHours(3)), closedByName = "Ana", total = 30m, pointsAwarded = 375,
            chargeId = tabCharge, items = new[] { new { id = Guid.NewGuid(), name = "Negroni", quantity = 2m, unitPrice = 15m, addedAtUtc = Iso(old) } },
        });
        s.Doc(s.OrgA, "charges", new
        {
            memberId = marie, memberName = "Marie Dupont", kind = "Bar", description = "Library Bar tab (1 items)", amount = 30m, paidAmount = 15m,
            status = "PartiallyPaid", postedAtUtc = Iso(old.AddHours(3)),
        }, tabCharge);
        s.Charges[tabCharge] = ("PartiallyPaid", 15m);
        s.Doc(s.OrgA, "payments", new
        {
            memberId = marie, memberName = "Marie Dupont", amount = 15m, method = "Cash", chargeId = tabCharge, receivedAtUtc = Iso(old.AddDays(1)),
        });
        var sharedItemId = Guid.NewGuid();
        s.Doc(s.OrgA, "tabs", new
        {
            memberId = marie, memberName = "Marie Dupont", memberTier = "Silver", venue = "Dining", status = "Open", openedAtUtc = Iso(now.AddHours(-1)),
            total = 42m,
            items = new object[]
            {
                new { id = sharedItemId, name = "Risotto", quantity = 1m, unitPrice = 28m, addedAtUtc = Iso(now.AddMinutes(-50)) },
                new { id = Guid.NewGuid(), name = "Wine", quantity = 2m, unitPrice = 7m, addedAtUtc = Iso(now.AddMinutes(-40)) },
            },
        });
        s.Doc(s.OrgA, "tabs", new
        {
            memberId = marie, memberName = "Marie Dupont", memberTier = "Silver", venue = "Spa", status = "Voided", openedAtUtc = Iso(old),
            voidReason = "Opened on the wrong member", voidedAtUtc = Iso(old.AddMinutes(5)), items = Array.Empty<object>(),
        });

        // Luc has nothing; a payment against a charge someone deleted long ago is on a member
        // whose balance never counted it.
        s.Member(s.OrgA, "Luc", 0m, 0m, tier: "Bronze");
        var paulo = s.Paulo = s.Member(s.OrgA, "Paulo", 0m, 0m, tier: "Bronze");
        s.Doc(s.OrgA, "payments", new { memberId = paulo, memberName = "Paulo Dupont", amount = 40m, method = "Cash", chargeId = Guid.NewGuid() });

        s.Doc(s.OrgA, "subscriptions", new
        {
            memberId = jean, memberName = "Jean Dupont", name = "Gold dues", amount = 299m, cadence = "Monthly", status = "Active",
            startedAtUtc = Iso(old), nextChargeAtUtc = Iso(now.AddDays(5)), lastChargedAtUtc = Iso(old),
        });
        s.Doc(s.OrgA, "subscriptions", new { memberId = marie, memberName = "Marie Dupont", name = "Locker", amount = 25m, cadence = "Quarterly", status = "Cancelled" });
        s.Doc(s.OrgA, "donations", new { memberId = jean, memberName = "Jean Dupont", cause = "Scholarship", amount = 500m, acknowledged = true, receivedAtUtc = Iso(old) });
        s.Doc(s.OrgA, "donations", new { memberId = marie, memberName = "Marie Dupont", cause = "Arts", amount = 75m, isAnonymous = true });

        // Gift cards: one part spent, one spent in full over two redemptions, one past its
        // expiry, one voided with what was left written off, one whose remaining value the
        // client once set by hand, and two that came in with the same code.
        Guid Card(decimal face, decimal left, string status, string code, DateTime? expires = null, decimal? writtenOff = null)
        {
            var id = s.Doc(s.OrgA, "gift-cards", new
            {
                code, purchaserName = "Ana", recipientName = "Bea", faceValue = face, remainingValue = left, status, issuedAtUtc = Iso(old),
                expiresAtUtc = expires is { } e ? Iso(e) : null, writtenOffValue = writtenOff,
                voidReason = status == "Voided" ? "Reported stolen" : null,
            });
            s.CardsLeft[id] = status == "Voided" ? 0 : left;
            return id;
        }
        void Redemption(Guid card, decimal amount, string? key = null) =>
            s.Doc(s.OrgA, "gift-card-redemptions", new { giftCardId = card, amount, redeemedByName = "Front desk", redeemedAtUtc = Iso(old.AddDays(3)), idempotencyKey = key });

        var partly = Card(100m, 70m, "Active", "GIFT-AAAA-BBBB-CCCC");
        Redemption(partly, 30m, key: "retry-1");
        var spent = Card(50m, 0m, "Redeemed", "GIFT-DDDD-EEEE-FFFF");
        Redemption(spent, 20m);
        Redemption(spent, 30m);
        Card(80m, 80m, "Active", "GIFT-GGGG-HHHH-JJJJ", expires: now.AddDays(-1));
        var stolen = Card(60m, 0m, "Voided", "GIFT-KKKK-MMMM-NNNN", writtenOff: 50m);
        Redemption(stolen, 10m);
        Card(40m, 25m, "Active", "GIFT-PPPP-QQQQ-RRRR");
        Card(20m, 20m, "Active", "GIFT-SAME-CODE-0001");
        Card(30m, 30m, "Active", "gift-same-code-0001");

        // A copy of the club made with the org-clone tool: its tab carries the same item ids.
        var copy = s.Member(s.OrgB, "Jean", houseBalance: -12m, manualBalance: 0m);
        var copyCharge = s.Doc(s.OrgB, "charges", new { memberId = copy, memberName = "Jean Dupont", kind = "Bar", description = "Bar", amount = 12m, paidAmount = 0m, status = "Pending", postedAtUtc = Iso(old) });
        s.Charges[copyCharge] = ("Pending", 0);
        s.Doc(s.OrgB, "tabs", new
        {
            memberId = copy, memberName = "Jean Dupont", memberTier = "Gold", venue = "Dining", status = "Open", openedAtUtc = Iso(now),
            items = new[] { new { id = sharedItemId, name = "Risotto", quantity = 1m, unitPrice = 28m, addedAtUtc = Iso(now) } },
        });

        // Something that is not JSON at all stays where it is.
        s.Documents.Add((Guid.NewGuid(), s.OrgA, "charges", "not json"));
        return s;
    }

    private static readonly string[] LedgerTables =
    [
        "ClubHouseAccounts", "ClubCharges", "ClubPayments", "ClubPaymentAllocations", "ClubSubscriptions",
        "ClubGiftCards", "ClubGiftCardTransactions", "ClubTabs", "ClubTabItems", "ClubDonations",
    ];

    private static async Task SeedAsync(string database, Seed s)
    {
        var hash = new ACI.Infrastructure.Services.BcryptPasswordHasher().Hash(Password);
        var sql = new List<string>
        {
            $@"INSERT INTO [Users] ([Id], [Name], [Email], [PasswordHash], [TwoFactorEnabled], [CreatedAtUtc], [EmailVerifiedAtUtc])
               VALUES ('{s.UserId}', N'Treasurer', N'{s.Email}', N'{hash}', 0, SYSUTCDATETIME(), SYSUTCDATETIME());",
        };
        foreach (var (org, name) in new[] { (s.OrgA, "Pavillon46"), (s.OrgB, "Pavillon46 copy") })
        {
            sql.Add($@"INSERT INTO [Organizations] ([Id], [Name], [OwnerUserId], [CreatedAtUtc], [StaleDealDays]) VALUES ('{org}', N'{name}', '{s.UserId}', SYSUTCDATETIME(), 30);");
            sql.Add($@"INSERT INTO [OrganizationMembers] ([OrganizationId], [UserId], [Role], [JoinedAtUtc]) VALUES ('{org}', '{s.UserId}', N'Owner', SYSUTCDATETIME());");
        }
        sql.AddRange(s.Sql);
        foreach (var (id, org, kind, payload) in s.Documents)
            sql.Add($@"INSERT INTO [ClubDocuments] ([Id], [OrganizationId], [Kind], [Payload], [UpdatedAtUtc], [Version])
                       VALUES ('{id}', {(org == null ? "NULL" : $"'{org}'")}, N'{kind}', N'{payload.Replace("'", "''")}', SYSUTCDATETIME(), NEWID());");
        foreach (var statement in sql)
            await SqlServerApp.ExecuteAsync(database, statement);
    }

    private static async Task<Dictionary<string, int>> CountsAsync(string database)
    {
        var counts = new Dictionary<string, int>();
        foreach (var table in LedgerTables)
            counts[table] = (int)(await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [{table}]"))!;
        return counts;
    }

    private static async Task<decimal> LedgerBalanceAsync(string database, Guid memberId) =>
        (decimal)(await SqlServerApp.ScalarAsync(database,
            $@"SELECT ISNULL((SELECT SUM([Amount]) FROM [ClubPayments] WHERE [MemberId] = '{memberId}'), 0)
                    - ISNULL((SELECT SUM([Amount]) FROM [ClubCharges] WHERE [MemberId] = '{memberId}'), 0)"))!;

    private static async Task<List<JsonElement>> ReadAsync(HttpClient client, string path)
    {
        var res = await client.GetAsync(path);
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{path}: {body}");
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    }

    private static async Task<JsonElement> ReadOneAsync(HttpClient client, string path)
    {
        var res = await client.GetAsync(path);
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{path}: {body}");
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }

    private static async Task<decimal> CardValueAsync(string database, Guid cardId) =>
        (decimal)(await SqlServerApp.ScalarAsync(database,
            $"SELECT ISNULL(SUM([Amount]), 0) FROM [ClubGiftCardTransactions] WHERE [GiftCardId] = '{cardId}'"))!;

    [SqlServerFact]
    public async Task EveryBalanceAndCardValueIsTheSameAfterTheMove_AndASecondRunAddsNothing()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            var migrations = SqlServerSchema.AllMigrationIds();
            var ledger = migrations.Single(m => m.EndsWith("_AddClubMoneyLedger", StringComparison.Ordinal));
            await using (var db = SqlServerSchema.Context(database))
                await db.GetService<IMigrator>().MigrateAsync(migrations[migrations.ToList().IndexOf(ledger) - 1]);
            var club = Club();
            await SeedAsync(database, club);
            var documents = (int)(await SqlServerApp.ScalarAsync(database, "SELECT COUNT(*) FROM [ClubDocuments]"))!;

            // The migration production runs at start-up, after the ones before it.
            await SqlServerSchema.MigrateAloneAsync(database);

            foreach (var (member, balance) in club.Balances)
                (await LedgerBalanceAsync(database, member)).Should().Be(balance, $"member {member} had a house balance of {balance} before the move");
            foreach (var (card, left) in club.CardsLeft)
                (await CardValueAsync(database, card)).Should().Be(left, $"gift card {card} had {left} left before the move");
            (await SqlServerApp.ScalarAsync(database, "SELECT COUNT(*) FROM [ClubDocuments]")).Should().Be(documents, "the move copies; the documents stay");
            (await SqlServerApp.ScalarAsync(database, "SELECT COUNT(DISTINCT [Code]) FROM [ClubGiftCards]")).Should().Be(club.CardsLeft.Count,
                "two cards that came in with one code keep a code each");
            (await SqlServerApp.ScalarAsync(database, "SELECT COUNT(*) FROM [ClubTabItems]")).Should().Be(4, "the copied club's tab has its own item, not the original's");

            // Each charge is paid what its record said, by its payments, or by a credit from
            // before the ledger where no payment was kept; a voided one by nothing.
            foreach (var (charge, (_, paid)) in club.Charges)
                ((decimal)(await SqlServerApp.ScalarAsync(database,
                    $"SELECT ISNULL(SUM([Amount]), 0) FROM [ClubPaymentAllocations] WHERE [ChargeId] = '{charge}'"))!)
                    .Should().Be(paid, $"charge {charge} was paid {paid}");
            (await SqlServerApp.ScalarAsync(database, "SELECT COUNT(*) FROM [ClubPayments] WHERE [Description] = N'Paid before the ledger'"))
                .Should().Be(1, "only the spa visit was marked paid with no payment kept");
            (await SqlServerApp.ScalarAsync(database, "SELECT COUNT(*) FROM [ClubCharges] WHERE [EntryType] = N'Void'")).Should().Be(1);
            (await SqlServerApp.ScalarAsync(database, "SELECT COUNT(*) FROM [ClubPayments] WHERE [EntryType] = N'Void'")).Should().Be(1, "the bounced cheque");

            // Opening balances are entries of their own; the only adjustments are the 10 the old
            // code dropped from Jean's overpayment and Paulo's payment against a deleted charge.
            (await SqlServerApp.ScalarAsync(database, "SELECT SUM([Amount]) FROM [ClubCharges] WHERE [EntryType] = N'Opening' AND [Description] = N'Opening balance brought over'"))
                .Should().Be(500m);
            (await SqlServerApp.ScalarAsync(database, "SELECT SUM([Amount]) FROM [ClubPayments] WHERE [EntryType] = N'Opening' AND [Description] = N'Opening balance brought over'"))
                .Should().Be(50m);
            (await SqlServerApp.ScalarAsync(database, "SELECT COUNT(*) FROM [ClubPayments] WHERE [EntryType] = N'Adjustment'")).Should().Be(0);
            (await SqlServerApp.ScalarAsync(database, "SELECT SUM([Amount]) FROM [ClubCharges] WHERE [EntryType] = N'Adjustment'")).Should().Be(50m);
            (await SqlServerApp.ScalarAsync(database, "SELECT COUNT(*) FROM [ClubGiftCardTransactions] WHERE [Type] = N'Adjustment'"))
                .Should().Be(1, "only the card whose remaining value was once set by hand is out of step with its redemptions");
            (await SqlServerApp.ScalarAsync(database, "SELECT -[Amount] FROM [ClubGiftCardTransactions] WHERE [Type] = N'Void'"))
                .Should().Be(50m, "the void writes off what was left on the card");

            // A second run, as an idempotent migration script would make, adds nothing.
            var counts = await CountsAsync(database);
            await using (var db = SqlServerSchema.Context(database))
                await db.Database.ExecuteSqlRawAsync(AddClubMoneyLedger.MoveDocumentsSql);
            (await CountsAsync(database)).Should().Equal(counts);

            // And through the API, as the club's screens read it once the app starts.
            using var app = SqlServerApp.Start(database);
            var client = app.CreateClient();
            var login = await client.PostAsJsonAsync("/api/auth/login", new { email = club.Email, password = Password });
            var loginBody = await login.Content.ReadAsStringAsync();
            login.IsSuccessStatusCode.Should().BeTrue(loginBody);
            using (var token = JsonDocument.Parse(loginBody))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.RootElement.GetProperty("token").GetString());
            foreach (var org in new[] { club.OrgA, club.OrgB })
            {
                client.DefaultRequestHeaders.Remove("X-Organization-Id");
                client.DefaultRequestHeaders.Add("X-Organization-Id", org.ToString());
                foreach (var member in await ReadAsync(client, "/api/members"))
                    member.GetProperty("houseAccountBalance").GetDecimal().Should().Be(club.Balances[member.GetProperty("id").GetGuid()],
                        "the members list shows the balance the club saw before the move");
                var listed = await ReadAsync(client, "/api/charges");
                foreach (var charge in listed.Where(c => c.GetProperty("entryType").GetString() == "Charge"))
                {
                    var (status, paid) = club.Charges[charge.GetProperty("id").GetGuid()];
                    charge.GetProperty("status").GetString().Should().Be(status);
                    charge.GetProperty("paidAmount").GetDecimal().Should().Be(paid);
                }
                // What was owed before the ledger is listed with the charges, to be paid like one.
                // The adjustments for money the old records dropped (Jean's 10, Paulo's 40)
                // are paid by that money: listed unpaid, they had the club chasing Paulo for
                // 40 with a balance of 0, and the outstanding total counted it.
                listed.Where(c => c.GetProperty("entryType").GetString() != "Charge")
                    .Select(c => (c.GetProperty("entryType").GetString(), c.GetProperty("amount").GetDecimal(), c.GetProperty("status").GetString()))
                    .Should().BeEquivalentTo(org == club.OrgA
                        ? new[] { ("Opening", 500m, "Pending"), ("Adjustment", 10m, "Paid"), ("Adjustment", 40m, "Paid") }
                        : Array.Empty<(string, decimal, string)>());
            }
            client.DefaultRequestHeaders.Remove("X-Organization-Id");
            client.DefaultRequestHeaders.Add("X-Organization-Id", club.OrgA.ToString());
            // What a member is shown owing and holding in credit, not only the balance they make:
            // Jean owes his bar tab, his dues and his opening balance, and Paulo nothing.
            foreach (var (member, owed, credit) in new[] { (club.Jean, 790m, 0m), (club.Paulo, 0m, 0m), (club.Marie, 15m, 50m) })
            {
                var balance = await ReadOneAsync(client, $"/api/members/{member}/balance");
                balance.GetProperty("owed").GetDecimal().Should().Be(owed, $"member {member} owes {owed}");
                balance.GetProperty("credit").GetDecimal().Should().Be(credit, $"member {member} holds {credit} of credit");
            }
            var cards = await ReadAsync(client, "/api/gift-cards");
            cards.Select(c => c.GetProperty("id").GetGuid()).Should().BeEquivalentTo(club.CardsLeft.Keys);
            foreach (var card in cards)
                card.GetProperty("remainingValue").GetDecimal().Should().Be(club.CardsLeft[card.GetProperty("id").GetGuid()]);
            cards.Select(c => c.GetProperty("status").GetString()).Should().BeEquivalentTo(
                new[] { "Active", "Redeemed", "Expired", "Voided", "Active", "Active", "Active" });
            (await ReadAsync(client, "/api/gift-cards/redemptions")).Should().HaveCount(4);
            var payments = await ReadAsync(client, "/api/payments");
            payments.Should().HaveCount(6, "every payment on record; the credit for the spa visit is not money received");
            payments.Count(p => p.TryGetProperty("status", out var st) && st.GetString() == "Voided").Should().Be(1);
            var tabs = await ReadAsync(client, "/api/tabs");
            tabs.Select(t => t.GetProperty("status").GetString()).Should().BeEquivalentTo(new[] { "Closed", "Open", "Voided" });
            tabs.Single(t => t.GetProperty("status").GetString() == "Open").GetProperty("total").GetDecimal().Should().Be(42);
            (await ReadAsync(client, "/api/subscriptions")).Should().HaveCount(2);
            (await ReadAsync(client, "/api/donations")).Should().HaveCount(2);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
