using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The step from a lead to a member: applications typed in, started from a lead or sent on
/// the public form, approved on the server in one go (the member, their links and consent,
/// the dues, the initiation fee, the household and the welcome), once however often it is
/// pressed, and approved by a countersigned contract too.
/// </summary>
/// <remarks>
/// Approval used to be three requests from the browser: a retry or two reviewers at once made
/// two members, and the new member was never billed and stayed Pending. The race at its worst
/// (two transactions on one row) is in ClubApplicationsOnSqlServerTests.
/// </remarks>
public class ClubApplicationsTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, string step, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(expected, $"{step} should answer {(int)expected} — got {(int)res.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<string> Refused(HttpResponseMessage res, HttpStatusCode status, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(status, $"{step} should be refused — got {(int)res.StatusCode}: {body}");
        return body;
    }

    private static async Task<(HttpClient Client, Guid UserId, Guid Org)> ClubAsync(CustomWebApplicationFactory factory, string email = "owner@pavillon46.ch")
    {
        var (client, userId) = await SignInAsync(factory, email);
        var org = await CreateAsync(factory, userId, "Pavillon46");
        client.In(org);
        return (client, userId, org);
    }

    private static async Task<HttpClient> JoinAsync(CustomWebApplicationFactory factory, HttpClient owner, Guid org, string email, string preset)
    {
        var (client, userId) = await SignInAsync(factory, email);
        await AddMemberAsync(factory, org, userId, OrgMemberRole.Member);
        await Json(await owner.PutAsync($"/api/organizations/{org}/members/{userId}/permissions", Body(new { preset })), $"make {email} {preset}");
        return client.In(org);
    }

    private static async Task<JsonElement> ApplyAsync(HttpClient client, string first = "Claire", string last = "Martin", string tier = "Gold", string? email = null) =>
        await Json(await client.PostAsync("/api/applications", Body(new
        {
            firstName = first, lastName = last, email = email ?? $"{first.ToLowerInvariant()}@example.ch", requestedTier = tier,
            reasonForJoining = "The chef series",
        })), "add application");

    private static Task<T> Read<T>(CustomWebApplicationFactory factory, Func<ACI.Infrastructure.Persistence.AppDbContext, T> read) =>
        factory.Services.Read(read);

    // ── approval ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApprovingMakesTheMember_WithTheirTiersDuesAndInitiationFee_AndQueuesAWelcome()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, org) = await ClubAsync(factory);
        var application = await ApplyAsync(client);
        var id = application.GetProperty("id").GetGuid();

        var result = await Json(await client.PostAsync($"/api/applications/{id}/approve", Body(new { reviewerNotes = "Lovely" })), "approve");

        result.GetProperty("memberCreated").GetBoolean().Should().BeTrue();
        result.GetProperty("approved").GetBoolean().Should().BeTrue();
        result.GetProperty("duesStarted").GetBoolean().Should().BeTrue();
        result.GetProperty("feeCharged").GetBoolean().Should().BeTrue();
        result.GetProperty("welcomeQueued").GetBoolean().Should().BeTrue();
        result.GetProperty("moneyPending").GetBoolean().Should().BeFalse();
        var memberId = result.GetProperty("memberId").GetGuid();
        result.GetProperty("application").GetProperty("status").GetString().Should().Be("Approved");
        result.GetProperty("application").GetProperty("memberId").GetGuid().Should().Be(memberId);

        var member = await Read(factory, db => db.ClubMembers.Single(m => m.Id == memberId));
        (member.FirstName, member.Tier, member.Status, member.DuesAmount, member.DuesFrequency)
            .Should().Be(("Claire", "Gold", "Active", 299m, "Monthly"), "an approved applicant is admitted at their tier's dues");
        var subscription = await Read(factory, db => db.ClubSubscriptions.Single(s => s.MemberId == memberId));
        (subscription.Name, subscription.Amount, subscription.Status).Should().Be(("Gold dues", 299m, "Active"));
        var fee = await Read(factory, db => db.ClubCharges.Single(c => c.MemberId == memberId));
        (fee.Description, fee.Amount, fee.IdempotencyKey).Should().Be(("Initiation fee, Gold", 1000m, $"initiation:{memberId}"));
        var welcome = await Read(factory, db => db.OutboundMessages.Single(m => m.Source == OutboundSources.Welcome));
        (welcome.To, welcome.MemberId, welcome.Purpose).Should().Be(("claire@example.ch", memberId, ConsentPurposes.Service));
        welcome.Subject.Should().Be("Welcome to Pavillon46");
        (await Read(factory, db => db.MemberMessages.Count(m => m.MemberId == memberId))).Should().Be(1, "the welcome shows in the member's communications");
    }

    [Fact]
    public async Task ApprovingAgain_OrSeveralAtOnce_MakesOneMemberOneFeeAndOneSubscription()
    {
        using var factory = new RaceFactory();
        var (client, _, _) = await ClubAsync(factory);
        var id = (await ApplyAsync(client)).GetProperty("id").GetGuid();
        // All four read the open application and decide to approve it before any of them claims it.
        factory.Gate.Arm("applications", readers: 4);

        var answers = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.PostAsync($"/api/applications/{id}/approve", Body(new { }))));
        var members = new HashSet<Guid>();
        foreach (var answer in answers)
            members.Add((await Json(answer, "approve at once")).GetProperty("memberId").GetGuid());
        var again = await Json(await client.PostAsync($"/api/applications/{id}/approve", Body(new { })), "approve again later");
        members.Add(again.GetProperty("memberId").GetGuid());
        again.GetProperty("approved").GetBoolean().Should().BeFalse("it was approved already: the member made then is the answer");

        members.Should().ContainSingle("every press answers with the one member");
        (await Read(factory, db => db.ClubMembers.Count())).Should().Be(1);
        (await Read(factory, db => db.ClubCharges.Count(c => c.Description.StartsWith("Initiation")))).Should().Be(1);
        (await Read(factory, db => db.ClubSubscriptions.Count())).Should().Be(1);
        (await Read(factory, db => db.OutboundMessages.Count(m => m.Source == OutboundSources.Welcome))).Should().Be(1);
    }

    [Fact]
    public async Task AReviewerWhoCannotChargeAccounts_ApprovesWithoutTheMoney_AndFinanceFinishesItOnce()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await ClubAsync(factory);
        var director = await JoinAsync(factory, owner, org, "director@pavillon46.ch", "membership_director");
        var id = (await ApplyAsync(director)).GetProperty("id").GetGuid();

        var approved = await Json(await director.PostAsync($"/api/applications/{id}/approve", Body(new { })), "the director approves");
        approved.GetProperty("memberCreated").GetBoolean().Should().BeTrue();
        approved.GetProperty("moneyPending").GetBoolean().Should().BeTrue();
        approved.GetProperty("duesStarted").GetBoolean().Should().BeFalse();
        (await Read(factory, db => db.ClubSubscriptions.Count() + db.ClubCharges.Count())).Should().Be(0, "no dues or fee without money.manage");

        var finished = await Json(await owner.PostAsync($"/api/applications/{id}/approve", Body(new { })), "the owner finishes it");
        finished.GetProperty("duesStarted").GetBoolean().Should().BeTrue();
        finished.GetProperty("feeCharged").GetBoolean().Should().BeTrue();
        finished.GetProperty("moneyPending").GetBoolean().Should().BeFalse();
        await Json(await owner.PostAsync($"/api/applications/{id}/approve", Body(new { })), "and once more");
        (await Read(factory, db => db.ClubSubscriptions.Count())).Should().Be(1);
        (await Read(factory, db => db.ClubCharges.Count())).Should().Be(1);
        (await Read(factory, db => db.ClubMembers.Count())).Should().Be(1);
    }

    [Fact]
    public async Task AWaitlistedApplication_CanBeApprovedOrReopened_ButARejectedOneMustBeReopenedFirst()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var waiting = (await ApplyAsync(client, "Mira")).GetProperty("id").GetGuid();
        var other = (await ApplyAsync(client, "Theo")).GetProperty("id").GetGuid();
        var rejected = (await ApplyAsync(client, "Naomi")).GetProperty("id").GetGuid();
        await Json(await client.PostAsync($"/api/applications/{waiting}/status", Body(new { status = "Waitlisted", reviewerNotes = "Tier full" })), "waitlist");
        await Json(await client.PostAsync($"/api/applications/{other}/status", Body(new { status = "Waitlisted" })), "waitlist another");
        await Json(await client.PostAsync($"/api/applications/{rejected}/status", Body(new { status = "Rejected" })), "reject");

        (await Json(await client.PostAsync($"/api/applications/{waiting}/approve", Body(new { })), "approve from the waitlist"))
            .GetProperty("memberCreated").GetBoolean().Should().BeTrue();
        (await Json(await client.PostAsync($"/api/applications/{other}/status", Body(new { status = "Submitted" })), "reopen the waitlisted one"))
            .GetProperty("status").GetString().Should().Be("Submitted");
        (await Refused(await client.PostAsync($"/api/applications/{rejected}/approve", Body(new { })), HttpStatusCode.Conflict, "approve a rejected one"))
            .Should().Contain("rejected");
        await Json(await client.PostAsync($"/api/applications/{rejected}/status", Body(new { status = "Submitted" })), "reopen the rejected one");
        await Json(await client.PostAsync($"/api/applications/{rejected}/approve", Body(new { })), "approve it now");
        (await Refused(await client.PutAsync($"/api/applications/{rejected}", Body(new { occupation = "Surgeon" })), HttpStatusCode.Conflict, "edit an approved one"))
            .Should().Contain("approved");
    }

    [Fact]
    public async Task SomeoneWhoIsAlreadyAMember_IsLinkedNotDuplicated_ButASpouseSharingTheMailboxGetsTheirOwnRecord()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var existing = (await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = "Peter", lastName = "Meier", email = "meier@example.ch", tier = "Silver", status = "Lapsed",
        })), "add the member")).GetProperty("id").GetGuid();

        var peter = (await ApplyAsync(client, "Peter", "Meier", email: "Meier@Example.ch")).GetProperty("id").GetGuid();
        var anna = (await ApplyAsync(client, "Anna", "Meier", email: "meier@example.ch")).GetProperty("id").GetGuid();

        var again = await Json(await client.PostAsync($"/api/applications/{peter}/approve", Body(new { })), "approve Peter");
        again.GetProperty("memberId").GetGuid().Should().Be(existing, "the same email and name is the same person");
        again.GetProperty("memberCreated").GetBoolean().Should().BeFalse();
        again.GetProperty("feeCharged").GetBoolean().Should().BeFalse("a returning member does not pay the joining fee again");
        (await Read(factory, db => db.ClubMembers.Single(m => m.Id == existing).Status)).Should().Be("Active", "approving them admits them again");

        var annaMember = await Json(await client.PostAsync($"/api/applications/{anna}/approve", Body(new { })), "approve Anna");
        annaMember.GetProperty("memberCreated").GetBoolean().Should().BeTrue("a different name at a shared address is a different person");
        (await Read(factory, db => db.ClubMembers.Count())).Should().Be(2);
    }

    [Fact]
    public async Task ApprovalJoinsTheHouseholdTheApplicationNames_AndCarriesTheLeadAndContact()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var peter = (await Json(await client.PostAsync("/api/members", Body(new { firstName = "Peter", lastName = "Meier", email = "peter@example.ch", tier = "Gold" })), "add Peter"))
            .GetProperty("id").GetGuid();
        var household = (await Json(await client.PostAsync("/api/households", Body(new { name = "Meier", primaryMemberId = peter })), "household")).GetProperty("id").GetGuid();
        var lead = (await Json(await client.PostAsync("/api/leads", Body(new { name = "Anna Meier", email = "anna@example.ch", status = "New" })), "lead")).GetProperty("id").GetGuid();

        var started = await Json(await client.PostAsync($"/api/applications/from-lead/{lead}", null), "start from the lead");
        started.GetProperty("leadId").GetGuid().Should().Be(lead);
        started.GetProperty("firstName").GetString().Should().Be("Anna");
        var contactId = started.GetProperty("contactId").GetGuid();
        (await Json(await client.PostAsync($"/api/applications/from-lead/{lead}", null), "start again")).GetProperty("id").GetGuid()
            .Should().Be(started.GetProperty("id").GetGuid(), "the open application is answered rather than a second made");
        var id = started.GetProperty("id").GetGuid();
        await Json(await client.PutAsync($"/api/applications/{id}", Body(new { householdId = household, householdRole = "Spouse", requestedTier = "Silver" })), "name the household");

        var result = await Json(await client.PostAsync($"/api/applications/{id}/approve", Body(new { rollUpBilling = true })), "approve");
        result.GetProperty("joinedHousehold").GetBoolean().Should().BeTrue();
        var memberId = result.GetProperty("memberId").GetGuid();
        var member = await Read(factory, db => db.ClubMembers.Single(m => m.Id == memberId));
        (member.LeadId, member.ContactId).Should().Be((lead, contactId), "the member links to the lead and contact they came in as");
        var link = await Read(factory, db => db.ClubHouseholdMembers.Single(l => l.MemberId == memberId));
        (link.HouseholdId, link.Role, link.RollUpBilling).Should().Be((household, "Spouse", true));
        // Her fee went to Peter: her bills roll up to him.
        var fee = await Read(factory, db => db.ClubCharges.Single(c => c.Description.StartsWith("Initiation")));
        (fee.MemberId, fee.IncurredByMemberId, fee.IncurredByName).Should().Be((peter, memberId, "Anna Meier"));

        // Both ways: the lead's and contact's pages see the member, and the member's sees them.
        var fromLead = await Json(await client.GetAsync($"/api/leads/{lead}/membership"), "the lead's membership");
        fromLead.GetProperty("members").EnumerateArray().Single().GetProperty("id").GetGuid().Should().Be(memberId);
        fromLead.GetProperty("household").GetProperty("id").GetGuid().Should().Be(household);
        var fromContact = await Json(await client.GetAsync($"/api/contacts/{contactId}/membership"), "the contact's membership");
        fromContact.GetProperty("members").EnumerateArray().Single().GetProperty("tier").GetString().Should().Be("Silver");
        var links = await Json(await client.GetAsync($"/api/members/{memberId}/links"), "the member's links");
        links.GetProperty("leadId").GetGuid().Should().Be(lead);
        links.GetProperty("contactName").GetString().Should().Be("Anna Meier");
        links.GetProperty("applications").EnumerateArray().Single().GetProperty("id").GetGuid().Should().Be(id);
    }

    [Fact]
    public async Task AMemberCannotBeLinkedToAnotherWorkspacesContactOrLead()
    {
        using var factory = new CustomWebApplicationFactory();
        var (mine, _, _) = await ClubAsync(factory);
        var (theirs, _, _) = await ClubAsync(factory, "other@club.ch");
        var foreignLead = (await Json(await theirs.PostAsync("/api/leads", Body(new { name = "Zoe Other", email = "zoe@example.ch", status = "New" })), "their lead"))
            .GetProperty("id").GetGuid();
        var memberId = (await Json(await mine.PostAsync("/api/members", Body(new { firstName = "Jean", lastName = "Dupont", email = "jean@example.ch" })), "member"))
            .GetProperty("id").GetGuid();

        await Refused(await mine.PutAsync($"/api/members/{memberId}", Body(new { leadId = foreignLead })), HttpStatusCode.BadRequest, "link their lead");
        await Refused(await mine.PostAsync("/api/applications", Body(new
        {
            firstName = "Zoe", lastName = "Other", email = "zoe@example.ch", requestedTier = "Gold", leadId = foreignLead,
        })), HttpStatusCode.BadRequest, "an application naming their lead");
        (await Read(factory, db => db.ClubMembers.Single(m => m.Id == memberId).LeadId)).Should().BeNull();

        // Another workspace's application is invisible and cannot be approved.
        var theirApplication = (await ApplyAsync(theirs, "Zoe")).GetProperty("id").GetGuid();
        await Refused(await mine.GetAsync($"/api/applications/{theirApplication}"), HttpStatusCode.NotFound, "read theirs");
        await Refused(await mine.PostAsync($"/api/applications/{theirApplication}/approve", Body(new { })), HttpStatusCode.NotFound, "approve theirs");
        (await Json(await mine.GetAsync("/api/applications"), "my list")).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task AViewerReadsApplicationsButCannotApprove_AndAFrontDeskCannotEither()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await ClubAsync(factory);
        var desk = await JoinAsync(factory, owner, org, "desk@pavillon46.ch", "front_desk");
        var id = (await ApplyAsync(owner)).GetProperty("id").GetGuid();

        (await Json(await desk.GetAsync("/api/applications"), "the desk reads")).GetArrayLength().Should().Be(1);
        await Refused(await desk.PostAsync($"/api/applications/{id}/approve", Body(new { })), HttpStatusCode.Forbidden, "the desk approves");
        await Refused(await desk.PutAsync("/api/applications/form", Body(new { isActive = true })), HttpStatusCode.Forbidden, "the desk opens the form");
        (await Read(factory, db => db.ClubMembers.Count())).Should().Be(0);
    }

    // ── a countersigned contract ─────────────────────────────────────────────

    [Fact]
    public async Task CountersigningTheMembershipContractOfALead_ApprovesTheirApplication()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, org) = await ClubAsync(factory);
        var lead = (await Json(await client.PostAsync("/api/leads", Body(new { name = "Jean-Michel Dupont", email = "jm@example.ch", status = "New" })), "lead"))
            .GetProperty("id").GetGuid();
        var application = await Json(await client.PostAsync($"/api/applications/from-lead/{lead}", null), "apply from the lead");
        await Json(await client.PutAsync($"/api/applications/{application.GetProperty("id").GetGuid()}", Body(new { requestedTier = "Silver" })), "pick the tier");

        var draft = await Json(await client.PostAsync("/api/contracts/draft", Body(new { leadId = lead, title = "Membership Agreement" })), "draft");
        var contractId = draft.GetProperty("id").GetString();
        await Json(await client.PutAsync($"/api/contracts/{contractId}", Body(new
        {
            title = "Membership Agreement", body = "MEMBERSHIP AGREEMENT\n\n1. Fees: CHF 129 a month.\n",
            counterpartyName = "Jean-Michel Dupont", counterpartyEmail = "jm@example.ch",
        })), "edit");
        var sent = await Json(await client.PostAsync($"/api/contracts/{contractId}/send", null), "send");
        var token = sent.GetProperty("signingUrl").GetString()!.Split("/sign/").Last();
        using var anon = factory.CreateClient();
        await Json(await anon.PostAsync($"/api/public/contracts/{token}/sign", Body(new { signatureName = "Jean-Michel Dupont", agreed = true })), "sign");

        (await Read(factory, db => db.ClubMembers.Count())).Should().Be(0, "signed by the applicant only, nobody is admitted yet");
        await Json(await client.PostAsync($"/api/contracts/{contractId}/countersign", Body(new { signatureName = "Club Secretary", agreed = true })), "countersign");

        var member = await Read(factory, db => db.ClubMembers.Single());
        (member.FirstName, member.LastName, member.Tier, member.LeadId).Should().Be(("Jean-Michel", "Dupont", "Silver", lead));
        var approved = await Read(factory, db => db.ClubApplications.Single());
        (approved.Status, approved.ApprovalTrigger, approved.ContractId?.ToString()).Should().Be(("Approved", ApprovalTriggers.Contract, contractId));
        (await Read(factory, db => db.ClubCharges.Count(c => c.MemberId == member.Id))).Should().Be(1, "the initiation fee, once");
        (await Read(factory, db => db.ClubSubscriptions.Count(s => s.MemberId == member.Id))).Should().Be(1, "the dues started");
    }

    [Fact]
    public async Task AContractWithNoApplicationBehindIt_AdmitsNobody()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, userId, org) = await ClubAsync(factory);
        var lead = (await Json(await client.PostAsync("/api/leads", Body(new { name = "Dining Client", email = "dining@example.ch", status = "New" })), "lead"))
            .GetProperty("id").GetGuid();
        using var scope = factory.Services.CreateScope();
        var admissions = scope.ServiceProvider.GetRequiredService<ACI.Application.Services.IClubApplications>();
        using (scope.ServiceProvider.GetRequiredService<ACI.Application.Interfaces.ITenantContext>().ActFor(org, userId))
            (await admissions.AdmitForContractAsync(org, Guid.NewGuid(), lead, null, userId)).Should().BeEmpty();
        (await Read(factory, db => db.ClubMembers.Count())).Should().Be(0);
    }

    [Fact]
    public async Task APaidDeposit_ApprovesTheApplicationOnce_RecordingThePaymentThatDidIt()
    {
        // The hook Phase 3's online payments call when an applicant's deposit is paid.
        using var factory = new CustomWebApplicationFactory();
        var (client, userId, org) = await ClubAsync(factory);
        var id = (await ApplyAsync(client)).GetProperty("id").GetGuid();
        var payment = Guid.NewGuid();

        using var scope = factory.Services.CreateScope();
        var admissions = scope.ServiceProvider.GetRequiredService<ACI.Application.Services.IClubApplications>();
        using (scope.ServiceProvider.GetRequiredService<ACI.Application.Interfaces.ITenantContext>().ActFor(org, userId))
        {
            var first = await admissions.AdmitForDepositAsync(org, id, userId, payment);
            first.IsSuccess.Should().BeTrue(first.IsSuccess ? "" : first.Error.Description);
            (first.Value.Approved, first.Value.MemberCreated, first.Value.FeeCharged).Should().Be((true, true, true));
            // The provider's webhook delivered twice.
            var again = await admissions.AdmitForDepositAsync(org, id, userId, payment);
            (again.Value.Approved, again.Value.MemberId).Should().Be((false, first.Value.MemberId));
        }

        var application = await Read(factory, db => db.ClubApplications.Single(a => a.Id == id));
        (application.ApprovalTrigger, application.DepositPaymentId).Should().Be((ApprovalTriggers.Deposit, payment));
        (await Read(factory, db => db.ClubMembers.Count())).Should().Be(1);
        (await Read(factory, db => db.ClubCharges.Count())).Should().Be(1, "one initiation fee");
    }

    // ── the public form ──────────────────────────────────────────────────────

    [Fact]
    public async Task ThePublicForm_TakesAnApplicationWithoutSigningIn_AndRecordsTheConsentGiven()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, ownerId, org) = await ClubAsync(factory);
        (await Json(await client.GetAsync("/api/applications/form"), "no form yet")).GetProperty("form").ValueKind.Should().Be(JsonValueKind.Null);
        var form = await Json(await client.PutAsync("/api/applications/form", Body(new { isActive = true, intro = "Join us" })), "set up the form");
        var token = form.GetProperty("publicToken").GetString();
        form.GetProperty("publicPath").GetString().Should().Be($"/apply/{token}");

        using var anon = factory.CreateClient();
        var page = await Json(await anon.GetAsync($"/api/public/apply/{token}"), "the visitor opens the form");
        page.GetProperty("clubName").GetString().Should().Be("Pavillon46");
        page.GetProperty("tiers").EnumerateArray().Select(t => t.GetProperty("name").GetString()).Should().Contain(["Silver", "Gold"]);

        var receipt = await Json(await anon.PostAsync($"/api/public/apply/{token}", Body(new
        {
            firstName = "Lena", lastName = "Weber", email = "lena@example.ch", requestedTier = "Gold", reasonForJoining = "Dining",
            marketingConsent = true,
        })), "apply", HttpStatusCode.Created);
        receipt.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
        receipt.TryGetProperty("id", out _).Should().BeFalse("the receipt says nothing about what was made");

        var application = await Read(factory, db => db.ClubApplications.Single());
        (application.OrganizationId, application.Status, application.Source, application.RequestedTier)
            .Should().Be((org, "Submitted", ApplicationSources.PublicForm, "Gold"));
        var lead = await Read(factory, db => db.Leads.Single());
        (lead.Email, lead.Source, lead.UserId).Should().Be(("lena@example.ch", "Membership application", ownerId), "a new lead, as the capture form makes them");
        application.LeadId.Should().Be(lead.Id);
        var consent = await Read(factory, db => db.ConsentRecords.Single());
        (consent.SubjectType, consent.SubjectId, consent.Granted, consent.Source, consent.Address)
            .Should().Be((ConsentSubjects.Application, application.Id, true, ConsentSources.Form, "lena@example.ch"));

        // Approved, the consent she gave is the member's, as given, on the form, then.
        var approved = await Json(await client.PostAsync($"/api/applications/{application.Id}/approve", Body(new { })), "approve");
        var memberId = approved.GetProperty("memberId").GetGuid();
        var memberConsent = await Json(await client.GetAsync($"/api/members/{memberId}/consent"), "the member's consent");
        var marketing = memberConsent.GetProperty("current").EnumerateArray()
            .Single(c => c.GetProperty("channel").GetString() == "email" && c.GetProperty("purpose").GetString() == "marketing");
        (marketing.GetProperty("state").GetString(), marketing.GetProperty("source").GetString()).Should().Be(("granted", "form"));
    }

    [Fact]
    public async Task ThePublicForm_LinksToTheLeadAlreadyThere_DropsBots_AndStopsWhenClosedOrGivenANewLink()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var lead = (await Json(await client.PostAsync("/api/leads", Body(new { name = "Theo Kim", email = "theo@example.ch", status = "New" })), "lead")).GetProperty("id").GetGuid();
        var token = (await Json(await client.PutAsync("/api/applications/form", Body(new { isActive = true })), "set up")).GetProperty("publicToken").GetString();
        using var anon = factory.CreateClient();

        await Json(await anon.PostAsync($"/api/public/apply/{token}", Body(new { firstName = "Theo", lastName = "Kim", email = "THEO@example.ch", requestedTier = "Gold" })),
            "Theo applies", HttpStatusCode.Created);
        (await Read(factory, db => db.ClubApplications.Single().LeadId)).Should().Be(lead, "the lead with that address is told, not duplicated");
        (await Read(factory, db => db.Leads.Count())).Should().Be(1);
        (await Read(factory, db => db.Activities.Count(a => a.LeadId == lead))).Should().BeGreaterThan(0, "the lead's timeline shows the application");

        await Json(await anon.PostAsync($"/api/public/apply/{token}", Body(new { firstName = "Bot", lastName = "Bot", email = "bot@example.ch", website = "http://spam" })),
            "a bot fills the hidden field", HttpStatusCode.Created);
        (await Read(factory, db => db.ClubApplications.Count())).Should().Be(1, "a filled honeypot is thanked and dropped");

        var rotated = (await Json(await client.PostAsync("/api/applications/form/rotate", null), "a new link")).GetProperty("publicToken").GetString();
        rotated.Should().NotBe(token);
        await Refused(await anon.GetAsync($"/api/public/apply/{token}"), HttpStatusCode.NotFound, "the old link");
        await Json(await client.PutAsync("/api/applications/form", Body(new { isActive = false })), "close the form");
        await Refused(await anon.GetAsync($"/api/public/apply/{rotated}"), HttpStatusCode.NotFound, "a closed form");
        await Refused(await anon.PostAsync($"/api/public/apply/{rotated}", Body(new { firstName = "Late", lastName = "Comer", email = "late@example.ch" })),
            HttpStatusCode.NotFound, "applying on a closed form");
    }

    [Fact]
    public async Task ThePublicForm_IsLimitedPerLink()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var token = (await Json(await client.PutAsync("/api/applications/form", Body(new { isActive = true })), "set up")).GetProperty("publicToken").GetString();
        using var anon = factory.CreateClient();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 12; i++)
            statuses.Add((await anon.PostAsync($"/api/public/apply/{token}", Body(new { firstName = $"P{i}", lastName = "X", email = $"p{i}@example.ch", website = "bot" }))).StatusCode);
        statuses.Should().Contain(HttpStatusCode.TooManyRequests, "a script posting in a loop is stopped");
    }
}
