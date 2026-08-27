using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ACI.WebApi.Tests;

/// <summary>
/// The whole contract, once, over real HTTP.
/// </summary>
/// <remarks>
/// <para>
/// Everything else about contracts is tested a piece at a time: the state machine
/// in isolation, the PDF bytes in isolation, the panel's behaviour against a mocked
/// client. All of those passed while the two most serious defects found in this
/// feature were live, because both were about what happens when the real pieces are
/// joined up — an accented counterparty name that made Kestrel throw on the
/// Content-Disposition header, and a token whose grace period was checked in the
/// wrong place.
/// </para>
/// <para>
/// So this walks the path a real user walks — register, workspace, lead, draft,
/// edit, send, sign as the counterparty, countersign, download — through the real
/// pipeline: routing, model binding, authorisation, the org header, serialisation.
/// One test, in order, because the steps genuinely depend on each other and a
/// contract that cannot be sent has nothing to sign.
/// </para>
/// <para>
/// The counterparty is deliberately "Jean-Michel Dupont" with an accented address:
/// the header defect only appeared for non-ASCII names, and a test using "Bob"
/// would have sailed past it.
/// </para>
/// </remarks>
public class ContractLifecycleEndToEndTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static async Task<JsonElement> ReadJson(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static StringContent Body(object value) =>
        new(JsonSerializer.Serialize(value, Json), Encoding.UTF8, "application/json");

    /// <summary>
    /// A workspace for the registered user, written straight to the database.
    /// </summary>
    /// <remarks>
    /// <para>
    /// NOT created over HTTP, and the reason is the test host rather than the
    /// product: creating a workspace backfills the new member's existing records
    /// with seven <c>ExecuteUpdateAsync</c> calls, and EF's in-memory provider has
    /// no SQL to translate them into, so it throws. SQLite would run them, but the
    /// model pins two columns to <c>nvarchar(max)</c>, which SQLite cannot parse —
    /// so there is no in-process provider that can serve the whole path.
    /// </para>
    /// <para>
    /// That costs nothing here: the subject is the contract lifecycle, and every
    /// contract endpoint still goes over real HTTP with a real JWT and a real
    /// membership check. The status list is seeded from LeadStatusVocabulary, the
    /// same source OrganizationService seeds from, so it cannot drift from what a
    /// real workspace gets.
    /// </para>
    /// </remarks>
    private static async Task<Guid> SeedWorkspace(
        CustomWebApplicationFactory factory, Guid ownerUserId, string name)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = name,
            OwnerUserId = ownerUserId,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Organizations.Add(org);
        db.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = org.Id,
            UserId = ownerUserId,
            Role = OrgMemberRole.Owner,
            JoinedAtUtc = DateTime.UtcNow,
        });

        var vocabulary = ACI.Domain.Common.LeadStatusVocabulary.Default;
        for (var i = 0; i < vocabulary.Length; i++)
        {
            db.LeadStatuses.Add(new LeadStatus
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                Name = vocabulary[i],
                DisplayOrder = i,
            });
        }

        await db.SaveChangesAsync();
        return org.Id;
    }

    [Fact]
    public async Task ADraftBecomesAnExecutedContract()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        // ── Register. The token comes back from registration, so there is no
        //    separate login step for a brand-new account.
        var reg = await ReadJson(await client.PostAsync("/api/auth/register", Body(new
        {
            email = "kia@bonapp.group",
            password = "correct horse battery staple",
            name = "Kia Jam",
        })), "register");

        var token = reg.GetProperty("token").GetString();
        token.Should().NotBeNullOrWhiteSpace();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var userId = reg.GetProperty("user").GetProperty("id").GetGuid();

        // ── A workspace, with the lead statuses the pipeline needs in order to
        //    move a status at all. See SeedWorkspace for why this one step is not
        //    driven over HTTP.
        var orgId = await SeedWorkspace(factory, userId, "Club Nautique du Léman");
        client.DefaultRequestHeaders.Add("X-Organization-Id", orgId.ToString());

        // The statuses this workspace was seeded with. An organisation with none is
        // exactly the state that left every lead stuck on "New", so assert the seed
        // actually happened rather than assuming it.
        // The status list, as the client actually receives it. This endpoint used to
        // answer with the Result WRAPPER rather than the array, which the frontend
        // read as an empty list — so every pipeline step held its status write back
        // and leads sat on "New" forever. Assert the SHAPE, not just the contents:
        // the contents were always right, inside an object nobody could read.
        var statuses = await ReadJson(await client.GetAsync("/api/leadstatuses"), "list lead statuses");
        statuses.ValueKind.Should().Be(JsonValueKind.Array,
            "the client does `Array.isArray(list) ? list : []`, so an object here is "
            + "silently an empty status list and nothing can derive a status");
        var names = statuses.EnumerateArray().Select(s => s.GetProperty("name").GetString()).ToArray();
        names.Should().Contain("Attempted Contact")
            .And.Contain("Awaiting Signature")
            .And.Contain("Signed");

        // ── A lead to attach the contract to.
        var lead = await ReadJson(await client.PostAsync("/api/leads", Body(new
        {
            name = "Jean-Michel Dupont",
            email = "jean-michel@example.ch",
            phone = "+41790000000",
            source = "Website",
            status = "New",
        })), "create lead");
        var leadId = lead.GetProperty("id").GetString();

        // ── Step 1: the draft.
        var draft = await ReadJson(await client.PostAsync("/api/contracts/draft", Body(new
        {
            leadId,
            title = "Membership Agreement — Jean-Michel Dupont",
        })), "create draft");

        var contractId = draft.GetProperty("id").GetString();
        draft.GetProperty("status").GetString().Should().Be("draft");
        draft.GetProperty("body").GetString().Should().NotBeNullOrWhiteSpace(
            "a draft with no body is nothing to sign");

        // ── Step 2: edit it. This is also where the counterparty email is set, and
        //    a contract cannot be sent without one.
        var edited = await ReadJson(await client.PutAsync($"/api/contracts/{contractId}", Body(new
        {
            title = "Membership Agreement — Jean-Michel Dupont",
            body = "MEMBERSHIP AGREEMENT\n====================\n\n"
                + "1. FEES\n-------\nCHF 1'450.00 per year, payable annually in advance.\n",
            counterpartyName = "Jean-Michel Dupont",
            counterpartyEmail = "jean-michel@example.ch",
        })), "edit draft");
        edited.GetProperty("status").GetString().Should().Be("draft", "editing must not send it");
        edited.GetProperty("body").GetString().Should().Contain("1'450.00");

        // ── Step 3: send it.
        var sent = await ReadJson(
            await client.PostAsync($"/api/contracts/{contractId}/send", content: null),
            "send for signature");

        // emailSent is FALSE here and that is correct: no SMTP is configured in a
        // test host. The contract must still be sent and the link still live —
        // reporting an email nobody received is the failure mode this field exists
        // to prevent, so assert the link came back regardless.
        var signingUrl = sent.GetProperty("signingUrl").GetString();
        signingUrl.Should().NotBeNullOrWhiteSpace(
            "the URL is the fallback when nobody could be emailed");

        var signToken = signingUrl!.Split("/sign/").Last();
        signToken.Should().NotBeNullOrWhiteSpace();

        // ── The stranger's view. No Authorization header at all: this is the one
        //    endpoint a person outside the organisation reaches, and it must work
        //    without a session.
        using var anon = factory.CreateClient();
        var publicView = await ReadJson(await anon.GetAsync($"/api/public/contracts/{signToken}"), "public view");
        publicView.GetProperty("canSign").GetBoolean().Should().BeTrue();
        publicView.GetProperty("body").GetString().Should().Contain("MEMBERSHIP AGREEMENT");
        publicView.GetProperty("organizationName").GetString().Should().Be("Club Nautique du Léman");
        // The counterparty may read it; they may not see the CRM's internals.
        publicView.TryGetProperty("events", out _).Should().BeFalse(
            "the audit trail is ours, not theirs");

        // ── Step 4: they sign.
        var signed = await ReadJson(await anon.PostAsync($"/api/public/contracts/{signToken}/sign", Body(new
        {
            signatureName = "Jean-Michel Dupont",
            agreed = true,
        })), "counterparty signs");
        signed.GetProperty("status").GetString().Should().Be("signed_by_client");
        signed.GetProperty("canSign").GetBoolean().Should().BeFalse("signing twice is not a thing");

        // ── Step 5: we countersign, which executes it.
        var executed = await ReadJson(await client.PostAsync($"/api/contracts/{contractId}/countersign", Body(new
        {
            signatureName = "Kia Jam",
            agreed = true,
        })), "countersign");
        executed.GetProperty("status").GetString().Should().Be("countersigned");
        executed.GetProperty("clientSignatureName").GetString().Should().Be("Jean-Michel Dupont");
        executed.GetProperty("counterSignatureName").GetString().Should().Be("Kia Jam");

        // ── The PDF, from both routes. This is the step that used to return 500:
        //    the filename is built from the counterparty's name, and a non-ASCII
        //    byte in Content-Disposition makes Kestrel throw.
        foreach (var (label, http, url) in new[]
        {
            ("CRM", client, $"/api/contracts/{contractId}/pdf"),
            ("public", anon, $"/api/public/contracts/{signToken}/pdf"),
        })
        {
            var pdf = await http.GetAsync(url);
            var detail = pdf.IsSuccessStatusCode ? "" : $": {await pdf.Content.ReadAsStringAsync()}";
            pdf.StatusCode.Should().Be(HttpStatusCode.OK, $"the {label} PDF should render{detail}");
            pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");

            var bytes = await pdf.Content.ReadAsByteArrayAsync();
            Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-", $"the {label} route should return a real PDF");
            Encoding.ASCII.GetString(bytes[^1024..]).Should().Contain("%%EOF", "a truncated PDF opens nowhere");
            bytes.Length.Should().BeGreaterThan(2000, "a PDF this small has lost its content");

            // RFC 6266: the accented name must survive as filename*, with an
            // ASCII-folded `filename` for older clients.
            var disposition = pdf.Content.Headers.ContentDisposition!.ToString();
            disposition.Should().Contain("filename*=", "the real name needs the encoded form");
            disposition.Should().Contain("Dupont");
        }

        // ── And the audit trail records the whole thing, in order.
        var final = await ReadJson(await client.GetAsync($"/api/contracts/{contractId}"), "fetch with events");
        var trail = final.GetProperty("events").EnumerateArray().ToArray();
        var events = trail.Select(e => e.GetProperty("type").GetString()).ToArray();
        events.Should().ContainInOrder("created", "edited", "sent", "viewed", "signed", "countersigned");

        // Nothing was emailed — no SMTP host in a test host — and the contract must
        // say so rather than implying a copy is on its way. The 'emailed' event is
        // recorded either way because it means "distribution was attempted"; the
        // outcome lives in its note and in the stamp.
        final.TryGetProperty("executedCopySentAtUtc", out var stamp).Should().BeTrue();
        stamp.ValueKind.Should().Be(JsonValueKind.Null,
            "the stamp is what tells the UI a copy really went, and none did");

        var emailed = trail.Single(e => e.GetProperty("type").GetString() == "emailed");
        emailed.GetProperty("detail").GetString().Should().Contain("could NOT be sent",
            "a trail that reads 'sent' for an email nobody received is worse than no trail");
    }

    [Fact]
    public async Task DecliningIsTerminalAndNothingFollowsIt()
    {
        // Declining has no undo anywhere in the product, so what must be true is
        // that afterwards nothing else is possible — not signing, and not a second
        // decline. Worth its own pass because it is a different terminal state and
        // the token has to stop working for the right reason.
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var reg = await ReadJson(await client.PostAsync("/api/auth/register", Body(new
        {
            email = "kia+decline@bonapp.group",
            password = "correct horse battery staple",
            name = "Kia Jam",
        })), "register");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", reg.GetProperty("token").GetString());

        var orgId = await SeedWorkspace(
            factory, reg.GetProperty("user").GetProperty("id").GetGuid(), "Club Nautique");
        client.DefaultRequestHeaders.Add("X-Organization-Id", orgId.ToString());

        var lead = await ReadJson(await client.PostAsync("/api/leads", Body(new
        {
            name = "Anaïs Berger", email = "anais@example.ch", source = "Referral", status = "New",
        })), "create lead");

        var draft = await ReadJson(await client.PostAsync("/api/contracts/draft", Body(new
        {
            leadId = lead.GetProperty("id").GetString(), title = "Membership Agreement",
        })), "draft");
        var contractId = draft.GetProperty("id").GetString();

        await ReadJson(await client.PutAsync($"/api/contracts/{contractId}", Body(new
        {
            body = "MEMBERSHIP AGREEMENT\n====================\n\nThe terms.\n",
            counterpartyName = "Anaïs Berger",
            counterpartyEmail = "anais@example.ch",
        })), "edit");

        var sent = await ReadJson(
            await client.PostAsync($"/api/contracts/{contractId}/send", content: null), "send");
        var signToken = sent.GetProperty("signingUrl").GetString()!.Split("/sign/").Last();

        using var anon = factory.CreateClient();
        var declined = await ReadJson(await anon.PostAsync($"/api/public/contracts/{signToken}/decline", Body(new
        {
            reason = "the fee is more than we budgeted for",
        })), "decline");
        declined.GetProperty("status").GetString().Should().Be("declined");
        declined.GetProperty("canSign").GetBoolean().Should().BeFalse();

        // Nothing follows. Both of these must be refused, and by the contract's
        // state rather than by the token having been quietly deleted — the page
        // still has to be able to READ a declined contract to say so.
        var signAfter = await anon.PostAsync($"/api/public/contracts/{signToken}/sign", Body(new
        {
            signatureName = "Anaïs Berger", agreed = true,
        }));
        signAfter.StatusCode.Should().Be(HttpStatusCode.BadRequest, "a declined contract cannot be signed");

        var declineAgain = await anon.PostAsync($"/api/public/contracts/{signToken}/decline", Body(new
        {
            reason = "changed my mind about my mind",
        }));
        declineAgain.StatusCode.Should().Be(HttpStatusCode.BadRequest, "declining twice is not a thing");

        var stillReadable = await ReadJson(
            await anon.GetAsync($"/api/public/contracts/{signToken}"), "read after declining");
        stillReadable.GetProperty("status").GetString().Should().Be("declined",
            "the page has to be able to tell them what happened");
    }
}
