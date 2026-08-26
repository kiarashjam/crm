using ACI.Application.Common;
using ACI.Application.Configuration;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ACI.Application.Tests.Services;

/// <summary>
/// The contract flow, end to end, against mocked storage.
/// </summary>
/// <remarks>
/// The state machine is tested on its own; this is about the ORCHESTRATION around
/// it — that sending freezes the body, that a failed email is reported instead of
/// swallowed, that the lead's pipeline actually moves, that voiding kills the link.
/// Those are the parts a passing state machine says nothing about.
/// </remarks>
public class ContractServiceTests
{
    private readonly Mock<IContractRepository> _contracts = new();
    private readonly Mock<ILeadRepository> _leads = new();
    private readonly Mock<IOrganizationRepository> _orgs = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IEmailSender> _email = new();
    private readonly ContractService _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrgId = Guid.NewGuid();
    private static readonly Guid LeadId = Guid.NewGuid();

    /// <summary>The in-memory store the mocks read and write.</summary>
    private readonly List<Contract> _store = new();
    private readonly List<ContractEvent> _events = new();

    public ContractServiceTests()
    {
        _contracts.Setup(r => r.AddAsync(It.IsAny<Contract>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Contract c, CancellationToken _) => { _store.Add(c); return c; });
        // True = the save landed. The tests that exercise a lost race override this.
        _contracts.Setup(r => r.UpdateAsync(It.IsAny<Contract>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _contracts.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, Guid org, CancellationToken _) =>
                _store.FirstOrDefault(c => c.Id == id && c.OrganizationId == org));
        _contracts.Setup(r => r.GetBySigningTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string hash, CancellationToken _) =>
                _store.FirstOrDefault(c => c.SigningTokenHash == hash));
        _contracts.Setup(r => r.AddEventAsync(It.IsAny<ContractEvent>(), It.IsAny<CancellationToken>()))
            .Returns((ContractEvent e, CancellationToken _) => { _events.Add(e); return Task.CompletedTask; });
        _contracts.Setup(r => r.GetEventsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) =>
                (IReadOnlyList<ContractEvent>)_events.Where(e => e.ContractId == id).ToList());

        _orgs.Setup(r => r.GetByIdAsync(OrgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Organization { Id = OrgId, Name = "Pavillon 46" });
        _users.Setup(r => r.GetByIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = UserId, Name = "Kia", Email = "kia@bonapp.group" });

        // Everything sends, unless a test says otherwise.
        _email.Setup(e => e.SendContractForSignatureEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<EmailAttachment?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _email.Setup(e => e.SendContractSignedNotificationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _email.Setup(e => e.SendExecutedContractEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<EmailAttachment?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        SetLead(new Lead { Id = LeadId, Name = "Jean Dupont", Email = "jean@example.com" });

        _sut = new ContractService(
            _contracts.Object, _leads.Object, _orgs.Object, _users.Object, _email.Object,
            Options.Create(new EmailSettings { FrontendBaseUrl = "https://crm.example.com" }),
            Mock.Of<ILogger<ContractService>>());
    }

    private Lead _lead = null!;

    private void SetLead(Lead lead)
    {
        _lead = lead;
        _leads.Setup(r => r.GetByIdAsync(LeadId, It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _lead);
        _leads.Setup(r => r.UpdateAsync(It.IsAny<Lead>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead l, Guid _, Guid? __, CancellationToken ___) => { _lead = l; return l; });
    }

    /// <summary>A draft with every placeholder filled, so it is actually sendable.</summary>
    private async Task<ContractDto> DraftReadyToSendAsync()
    {
        var result = await _sut.CreateDraftAsync(UserId, OrgId, new CreateContractDraftRequest
        {
            LeadId = LeadId,
            Values = ContractTemplate.FieldsUsed(ContractTemplate.DefaultTemplate)
                .ToDictionary(f => f, f => (string?)$"[{f}]"),
        });
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    /* --------------------------------------------------------- step 1: draft */

    [Fact]
    public async Task DraftMergesTheLeadIntoTheTemplate()
    {
        var result = await _sut.CreateDraftAsync(UserId, OrgId, new CreateContractDraftRequest { LeadId = LeadId });

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(ContractStatuses.Draft);
        result.Value.Body.Should().Contain("Jean Dupont").And.Contain("Pavillon 46");
        result.Value.CounterpartyEmail.Should().Be("jean@example.com");
    }

    [Fact]
    public async Task DraftReportsThePlaceholdersItCouldNotFill()
    {
        // The CRM user has to be told, because the alternative is a contract that
        // reads "The Member agrees to pay {{contract.fee}}".
        var result = await _sut.CreateDraftAsync(UserId, OrgId, new CreateContractDraftRequest { LeadId = LeadId });

        result.Value.UnresolvedFields.Should().Contain("contract.fee");
        result.Value.Body.Should().Contain("{{contract.fee}}");
    }

    [Fact]
    public async Task DraftRefusesALeadThatIsNotOurs()
    {
        _leads.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead?)null);

        var result = await _sut.CreateDraftAsync(UserId, OrgId, new CreateContractDraftRequest { LeadId = Guid.NewGuid() });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Contract.LeadNotFound");
    }

    /* ---------------------------------------------------------- step 2: edit */

    [Fact]
    public async Task DraftCanBeEdited()
    {
        var draft = await DraftReadyToSendAsync();
        var result = await _sut.UpdateDraftAsync(draft.Id, UserId, OrgId,
            new UpdateContractRequest { Body = "Agreed terms." });

        result.IsSuccess.Should().BeTrue();
        result.Value.Body.Should().Be("Agreed terms.");
    }

    [Fact]
    public async Task CannotEditOnceSent()
    {
        // The failure that would make everything else worthless: they sign one
        // text and we keep another.
        var draft = await DraftReadyToSendAsync();
        await _sut.SendAsync(draft.Id, UserId, OrgId, resend: false);

        var result = await _sut.UpdateDraftAsync(draft.Id, UserId, OrgId,
            new UpdateContractRequest { Body = "Different terms entirely." });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Contract.NotAllowedInThisState");
        _store.Single().Body.Should().NotContain("Different terms");
    }

    /* ---------------------------------------------------------- step 3: send */

    [Fact]
    public async Task SendingRefusesWhileAnyPlaceholderIsUnfilled()
    {
        var draft = await _sut.CreateDraftAsync(UserId, OrgId, new CreateContractDraftRequest { LeadId = LeadId });

        var result = await _sut.SendAsync(draft.Value.Id, UserId, OrgId, resend: false);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Contract.HasUnresolvedFields");
        _store.Single().Status.Should().Be(ContractStatuses.Draft);
    }

    [Fact]
    public async Task SendingRefusesWithoutAnEmailAddress()
    {
        var draft = await DraftReadyToSendAsync();
        await _sut.UpdateDraftAsync(draft.Id, UserId, OrgId, new UpdateContractRequest { CounterpartyEmail = "" });

        var result = await _sut.SendAsync(draft.Id, UserId, OrgId, resend: false);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Contract.CounterpartyEmailRequired");
    }

    [Fact]
    public async Task SendingFreezesTheBodyHashAndMintsAHashedToken()
    {
        var draft = await DraftReadyToSendAsync();
        var result = await _sut.SendAsync(draft.Id, UserId, OrgId, resend: false);

        result.IsSuccess.Should().BeTrue();
        var stored = _store.Single();
        stored.Status.Should().Be(ContractStatuses.Sent);
        stored.BodyHashAtSend.Should().Be(ContractSigningToken.HashBody(stored.Body));
        // Only the hash is kept. The raw token exists solely in the returned URL.
        stored.SigningTokenHash.Should().NotBeNullOrEmpty().And.HaveLength(64);
        stored.SigningTokenExpiresAtUtc.Should().BeAfter(DateTime.UtcNow);
        result.Value.SigningUrl.Should().StartWith("https://crm.example.com/sign/");
        stored.SigningTokenHash.Should().NotBe(
            result.Value.SigningUrl.Split('/').Last(), "the stored value must be the hash, not the token");
    }

    [Fact]
    public async Task SendingReportsAFAILEDEmailInsteadOfClaimingSuccess()
    {
        // The whole point of returning emailSent. A contract recorded as "sent" to
        // someone who was never told is the worst outcome available here.
        _email.Setup(e => e.SendContractForSignatureEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<EmailAttachment?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var draft = await DraftReadyToSendAsync();
        var result = await _sut.SendAsync(draft.Id, UserId, OrgId, resend: false);

        result.IsSuccess.Should().BeTrue("the contract IS sent — the link is live");
        result.Value.EmailSent.Should().BeFalse();
        result.Value.SigningUrl.Should().NotBeNullOrEmpty("so it can be passed on by hand");
        _events.Should().Contain(e => e.Type == "sent" && e.Detail!.Contains("could not be sent"));
    }

    [Fact]
    public async Task SendingAdvancesTheLeadPipelineWithoutWipingIt()
    {
        // Merging by key, not replacing: anything else would erase the outreach and
        // meeting phases the moment a contract went out.
        _lead.PipelineState = """{"outreachStatus":"contacted","meetingAttended":true}""";

        var draft = await DraftReadyToSendAsync();
        await _sut.SendAsync(draft.Id, UserId, OrgId, resend: false);

        _lead.PipelineState.Should().Contain("\"contractStatus\":\"yes\"");
        _lead.PipelineState.Should().Contain("contractSentDate");
        _lead.PipelineState.Should().Contain("\"outreachStatus\":\"contacted\"");
        _lead.PipelineState.Should().Contain("\"meetingAttended\":true");
    }

    [Fact]
    public async Task SendingSurvivesACorruptPipelineBlob()
    {
        _lead.PipelineState = "{not json at all";

        var draft = await DraftReadyToSendAsync();
        var result = await _sut.SendAsync(draft.Id, UserId, OrgId, resend: false);

        result.IsSuccess.Should().BeTrue();
        _lead.PipelineState.Should().Contain("contractStatus");
    }

    /* ------------------------------------------- step 3b: they sign, by token */

    private static string TokenFrom(SendContractResult r) => r.SigningUrl.Split('/').Last();

    [Fact]
    public async Task TheCounterpartySignsThroughTheLink()
    {
        var draft = await DraftReadyToSendAsync();
        var sent = await _sut.SendAsync(draft.Id, UserId, OrgId, resend: false);

        var result = await _sut.SignByTokenAsync(
            TokenFrom(sent.Value), new SignContractRequest { SignatureName = "Jean Dupont", Agreed = true },
            "203.0.113.7", "Mozilla/5.0");

        result.IsSuccess.Should().BeTrue();
        var stored = _store.Single();
        stored.Status.Should().Be(ContractStatuses.SignedByClient);
        stored.ClientSignatureName.Should().Be("Jean Dupont");
        stored.ClientSignatureIp.Should().Be("203.0.113.7");
        stored.ClientSignedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task SigningRequiresTheConsentTick()
    {
        // The tick is what turns a typed name into an act of agreement.
        var draft = await DraftReadyToSendAsync();
        var sent = await _sut.SendAsync(draft.Id, UserId, OrgId, resend: false);

        var result = await _sut.SignByTokenAsync(
            TokenFrom(sent.Value), new SignContractRequest { SignatureName = "Jean Dupont", Agreed = false }, null, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Contract.ConsentRequired");
        _store.Single().Status.Should().Be(ContractStatuses.Sent);
    }

    [Fact]
    public async Task SigningRejectsAPlaceholderMark()
    {
        var draft = await DraftReadyToSendAsync();
        var sent = await _sut.SendAsync(draft.Id, UserId, OrgId, resend: false);

        var result = await _sut.SignByTokenAsync(
            TokenFrom(sent.Value), new SignContractRequest { SignatureName = "x", Agreed = true }, null, null);

        result.Error.Code.Should().Be("Contract.SignatureNameRequired");
    }

    [Fact]
    public async Task AnUnknownTokenIsRefusedTheSameWayAsAMalformedOne()
    {
        // Identical answers, so the endpoint cannot be used to discover which
        // tokens exist.
        var unknown = await _sut.GetByTokenAsync("totally-made-up", null, null);
        var blank = await _sut.GetByTokenAsync("", null, null);

        unknown.Error.Code.Should().Be("Contract.LinkInvalid");
        blank.Error.Code.Should().Be("Contract.LinkInvalid");
    }

    [Fact]
    public async Task AnExpiredLinkIsRefused()
    {
        var draft = await DraftReadyToSendAsync();
        var sent = await _sut.SendAsync(draft.Id, UserId, OrgId, resend: false);
        _store.Single().SigningTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);

        var result = await _sut.GetByTokenAsync(TokenFrom(sent.Value), null, null);

        result.Error.Code.Should().Be("Contract.LinkExpired");
    }

    [Fact]
    public async Task OpeningTheLinkIsRecordedOnceAndTheViewDoesNotLeakInternals()
    {
        var draft = await DraftReadyToSendAsync();
        var sent = await _sut.SendAsync(draft.Id, UserId, OrgId, resend: false);
        var token = TokenFrom(sent.Value);

        var first = await _sut.GetByTokenAsync(token, "203.0.113.7", "Firefox");
        await _sut.GetByTokenAsync(token, "203.0.113.7", "Firefox");

        first.IsSuccess.Should().BeTrue();
        first.Value.CanSign.Should().BeTrue();
        first.Value.OrganizationName.Should().Be("Pavillon 46");
        _events.Count(e => e.Type == "viewed").Should().Be(1, "the first view is the interesting one");
    }

    /* --------------------------------------------------- step 4: countersign */

    [Fact]
    public async Task CANNOTCountersignBeforeTheyHaveSigned()
    {
        // Would produce an "executed" contract carrying only our own signature.
        var draft = await DraftReadyToSendAsync();
        await _sut.SendAsync(draft.Id, UserId, OrgId, resend: false);

        var result = await _sut.CountersignAsync(draft.Id, UserId, OrgId,
            new SignContractRequest { SignatureName = "Kia", Agreed = true }, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Contract.NotAllowedInThisState");
        _store.Single().CounterSignatureName.Should().BeNull();
    }

    [Fact]
    public async Task CountersigningExecutesItAndEmailsBothParties()
    {
        var draft = await DraftReadyToSendAsync();
        var sent = await _sut.SendAsync(draft.Id, UserId, OrgId, resend: false);
        await _sut.SignByTokenAsync(TokenFrom(sent.Value),
            new SignContractRequest { SignatureName = "Jean Dupont", Agreed = true }, null, null);

        var result = await _sut.CountersignAsync(draft.Id, UserId, OrgId,
            new SignContractRequest { SignatureName = "Kia", Agreed = true }, "198.51.100.4");

        result.IsSuccess.Should().BeTrue();
        var stored = _store.Single();
        stored.Status.Should().Be(ContractStatuses.Countersigned);
        stored.CounterSignatureName.Should().Be("Kia");
        stored.ExecutedCopySentAtUtc.Should().NotBeNull();

        // Both parties, and the body travels with it — inline, so the email is a
        // complete copy even if the attachment is stripped in transit.
        _email.Verify(e => e.SendExecutedContractEmailAsync(
            "jean@example.com", It.IsAny<string>(), "Pavillon 46", It.IsAny<string>(),
            It.Is<string>(b => b.Length > 0), It.Is<string>(s => s.Contains("SIGNATURE RECORD")),
            It.IsAny<EmailAttachment?>(), It.IsAny<CancellationToken>()), Times.Once);
        _email.Verify(e => e.SendExecutedContractEmailAsync(
            "kia@bonapp.group", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<EmailAttachment?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecutionAdvancesTheSignaturePhase()
    {
        var draft = await DraftReadyToSendAsync();
        var sent = await _sut.SendAsync(draft.Id, UserId, OrgId, resend: false);
        await _sut.SignByTokenAsync(TokenFrom(sent.Value),
            new SignContractRequest { SignatureName = "Jean Dupont", Agreed = true }, null, null);
        await _sut.CountersignAsync(draft.Id, UserId, OrgId,
            new SignContractRequest { SignatureName = "Kia", Agreed = true }, null);

        _lead.PipelineState.Should().Contain("\"contractSigned\":\"yes\"");
        _lead.PipelineState.Should().Contain("signatureDate");
    }

    [Fact]
    public async Task AHalfDeliveredExecutionIsNotStampedAsSent()
    {
        // Leaving the stamp null is what makes the retry button mean something.
        _email.Setup(e => e.SendExecutedContractEmailAsync(
                "kia@bonapp.group", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<EmailAttachment?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var draft = await DraftReadyToSendAsync();
        var sent = await _sut.SendAsync(draft.Id, UserId, OrgId, resend: false);
        await _sut.SignByTokenAsync(TokenFrom(sent.Value),
            new SignContractRequest { SignatureName = "Jean Dupont", Agreed = true }, null, null);
        var result = await _sut.CountersignAsync(draft.Id, UserId, OrgId,
            new SignContractRequest { SignatureName = "Kia", Agreed = true }, null);

        result.IsSuccess.Should().BeTrue("the contract IS executed; only delivery failed");
        _store.Single().ExecutedCopySentAtUtc.Should().BeNull();
    }

    /* -------------------------------------------------- declining and voiding */

    [Fact]
    public async Task DecliningClosesItAndRecordsTheirDecision()
    {
        var draft = await DraftReadyToSendAsync();
        var sent = await _sut.SendAsync(draft.Id, UserId, OrgId, resend: false);

        var result = await _sut.DeclineByTokenAsync(TokenFrom(sent.Value),
            new DeclineContractRequest { Reason = "Too expensive" }, null, null);

        result.IsSuccess.Should().BeTrue();
        _store.Single().Status.Should().Be(ContractStatuses.Declined);
        _lead.PipelineState.Should().Contain("\"contractSigned\":\"no\"");
    }

    [Fact]
    public async Task VoidingKillsTheSigningLink()
    {
        var draft = await DraftReadyToSendAsync();
        var sent = await _sut.SendAsync(draft.Id, UserId, OrgId, resend: false);
        var token = TokenFrom(sent.Value);

        await _sut.VoidAsync(draft.Id, UserId, OrgId, "Wrong terms");

        _store.Single().SigningTokenHash.Should().BeNull();
        // A stale copy of the link in an inbox must not still work.
        var afterVoid = await _sut.GetByTokenAsync(token, null, null);
        afterVoid.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task VoidingRecordsNOTHINGAgainstTheCustomer()
    {
        // Voiding is our decision. Writing contractSigned:"no" would report it as a
        // customer drop-out and corrupt the drop-off report.
        var draft = await DraftReadyToSendAsync();
        await _sut.SendAsync(draft.Id, UserId, OrgId, resend: false);
        var before = _lead.PipelineState;

        await _sut.VoidAsync(draft.Id, UserId, OrgId, null);

        // Unchanged by the void, and specifically carrying no NEGATIVE. It does
        // carry contractSigned:"pending" — written by the SEND, which is a fact
        // about where the contract got to, not a verdict on the customer.
        _lead.PipelineState.Should().Be(before);
        _lead.PipelineState.Should().NotContain("\"contractSigned\":\"no\"");
        _lead.PipelineState.Should().Contain("\"contractSigned\":\"pending\"");
    }

    /* ---------------------------------------------------------- audit trail */

    [Fact]
    public async Task TheWholeJourneyIsInTheAuditTrail()
    {
        var draft = await DraftReadyToSendAsync();
        var sent = await _sut.SendAsync(draft.Id, UserId, OrgId, resend: false);
        var token = TokenFrom(sent.Value);
        await _sut.GetByTokenAsync(token, "203.0.113.7", "Firefox");
        await _sut.SignByTokenAsync(token,
            new SignContractRequest { SignatureName = "Jean Dupont", Agreed = true }, "203.0.113.7", "Firefox");
        await _sut.CountersignAsync(draft.Id, UserId, OrgId,
            new SignContractRequest { SignatureName = "Kia", Agreed = true }, "198.51.100.4");

        _events.Select(e => e.Type).Should().ContainInOrder(
            "created", "sent", "viewed", "signed", "countersigned", "emailed");
        // The counterparty has no account, so they are attributable by label only.
        _events.Single(e => e.Type == "signed").ActorUserId.Should().BeNull();
        _events.Single(e => e.Type == "signed").ActorLabel.Should().Be("Jean Dupont");
        _events.Single(e => e.Type == "signed").Ip.Should().Be("203.0.113.7");
    }
    /* --------------------------------------------- the link's own lifetime */

    [Fact]
    public async Task DecliningShortensTheSigningLinkInsteadOfLeavingItLive()
    {
        // Found by auditing, and reproduced before it was fixed. Declined is terminal:
        // the state machine permits nothing from it, INCLUDING void, which is the only
        // thing that kills a link. So the full text of a declined agreement stayed
        // readable at an anonymous URL for the rest of the token's thirty days, and
        // the product offered no way at all to revoke it.
        var draft = await DraftReadyToSendAsync();
        var sent = await _sut.SendAsync(draft.Id, UserId, OrgId, resend: false);
        var token = TokenFrom(sent.Value);

        var beforeDecline = _store.Single(c => c.Id == draft.Id).SigningTokenExpiresAtUtc;
        beforeDecline.Should().BeAfter(DateTime.UtcNow.AddDays(29), "a fresh link lasts 30 days");

        await _sut.DeclineByTokenAsync(token,
            new DeclineContractRequest { Reason = "too expensive" }, "1.1.1.1", "UA");

        var after = _store.Single(c => c.Id == draft.Id);
        after.Status.Should().Be(ContractStatuses.Declined);
        after.SigningTokenExpiresAtUtc.Should().BeBefore(DateTime.UtcNow.AddDays(8));

        // And voiding is still refused, which is precisely why the expiry had to move:
        // there is no other lever.
        (await _sut.VoidAsync(draft.Id, UserId, OrgId, "kill the link")).IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task CountersigningShortensTheSigningLinkToo()
    {
        var draft = await DraftReadyToSendAsync();
        var sent = await _sut.SendAsync(draft.Id, UserId, OrgId, resend: false);
        await _sut.SignByTokenAsync(TokenFrom(sent.Value),
            new SignContractRequest { SignatureName = "Jean Dupont", Agreed = true }, null, null);
        await _sut.CountersignAsync(draft.Id, UserId, OrgId,
            new SignContractRequest { SignatureName = "Kia", Agreed = true }, null);

        _store.Single(c => c.Id == draft.Id).SigningTokenExpiresAtUtc
            .Should().BeBefore(DateTime.UtcNow.AddDays(8));
    }

    [Fact]
    public async Task TheGracePeriodStillLetsSomebodyReadWhatTheyJustSigned()
    {
        // Cutting the link dead on completion would be tidier and worse: clicking the
        // bookmark you signed from would report an invalid link rather than telling you
        // that you have already signed, which reads as a fault in the product.
        var draft = await DraftReadyToSendAsync();
        var sent = await _sut.SendAsync(draft.Id, UserId, OrgId, resend: false);
        var token = TokenFrom(sent.Value);
        await _sut.SignByTokenAsync(token,
            new SignContractRequest { SignatureName = "Jean Dupont", Agreed = true }, null, null);
        await _sut.CountersignAsync(draft.Id, UserId, OrgId,
            new SignContractRequest { SignatureName = "Kia", Agreed = true }, null);

        var read = await _sut.GetByTokenAsync(token, "1.1.1.1", "UA");
        read.IsSuccess.Should().BeTrue();
        read.Value.CanSign.Should().BeFalse();
        read.Value.Blocked.Should().Be("This contract is fully signed by both parties.");
    }

    [Fact]
    public async Task ShorteningTheLinkNeverExtendsIt()
    {
        // A link about to expire in an hour must not gain a week because the contract
        // was countersigned.
        var draft = await DraftReadyToSendAsync();
        var sent = await _sut.SendAsync(draft.Id, UserId, OrgId, resend: false);
        var token = TokenFrom(sent.Value);
        await _sut.SignByTokenAsync(token,
            new SignContractRequest { SignatureName = "Jean Dupont", Agreed = true }, null, null);

        var soon = DateTime.UtcNow.AddHours(1);
        _store.Single(c => c.Id == draft.Id).SigningTokenExpiresAtUtc = soon;

        await _sut.CountersignAsync(draft.Id, UserId, OrgId,
            new SignContractRequest { SignatureName = "Kia", Agreed = true }, null);

        _store.Single(c => c.Id == draft.Id).SigningTokenExpiresAtUtc.Should().Be(soon);
    }

    /* ------------------------------------------ the sixteen the audit found */

    [Fact]
    public async Task ALeadWithNoPhoneNumberStillProducesASendableDraft()
    {
        // The optional-clause pattern: `lead.phoneClause` is either ", +41 ..." or
        // nothing at all. Null meant UNRESOLVED, so every lead without a phone
        // number got a draft showing the literal "{{lead.phoneClause}}" — and
        // sending is refused while any placeholder is unresolved, so that contract
        // could never be sent at all.
        SetLead(new Lead { Id = LeadId, Name = "Jean Dupont", Email = "jean@example.com", Phone = null });

        var draft = await _sut.CreateDraftAsync(UserId, OrgId, new CreateContractDraftRequest
        {
            LeadId = LeadId,
            Values = ContractTemplate.FieldsUsed(ContractTemplate.DefaultTemplate)
                .Where(f => !f.StartsWith("lead.", StringComparison.Ordinal) && f != "org.name" && f != "today")
                .ToDictionary(f => f, f => (string?)$"[{f}]"),
        });

        draft.IsSuccess.Should().BeTrue();
        draft.Value.Body.Should().NotContain("{{lead.phoneClause}}");
        draft.Value.UnresolvedFields.Should().BeEmpty("a draft with no phone number must still be sendable");

        // And the line it belongs to still reads correctly — no dangling comma, and
        // no two fields running together.
        draft.Value.Body.Should().Contain("Email:     jean@example.com");

        var sent = await _sut.SendAsync(draft.Value.Id, UserId, OrgId, resend: false);
        sent.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task AGenuinelyMissingFieldIsStillReportedUnresolved()
    {
        // The optional list must not have made every blank acceptable. "We have an
        // empty string for their fee" and "we know the fee is nothing" are still
        // different claims, and only one belongs in a contract.
        var draft = await _sut.CreateDraftAsync(UserId, OrgId, new CreateContractDraftRequest
        {
            LeadId = LeadId,
        });
        draft.Value.UnresolvedFields.Should().Contain("contract.fee");

        var refused = await _sut.SendAsync(draft.Value.Id, UserId, OrgId, resend: false);
        refused.IsFailure.Should().BeTrue();
        refused.Error.Should().Be(DomainErrors.Contract.HasUnresolvedFields);
    }

    [Fact]
    public async Task SendingAReplacementClearsAnEarlierDecline()
    {
        // Contract A declined, contract B sent on the same lead. The lead used to
        // keep contractSigned:"no" for good, so it went on reading "Contract
        // declined" and Lost while a live signing link was out with the
        // counterparty. The send now supersedes the old outcome.
        var a = await DraftReadyToSendAsync();
        var sentA = await _sut.SendAsync(a.Id, UserId, OrgId, resend: false);
        await _sut.DeclineByTokenAsync(TokenFrom(sentA.Value),
            new DeclineContractRequest { Reason = "too expensive" }, null, null);
        _lead.PipelineState.Should().Contain("\"contractSigned\":\"no\"");

        var b = await DraftReadyToSendAsync();
        await _sut.SendAsync(b.Id, UserId, OrgId, resend: false);

        _lead.PipelineState.Should().NotContain("\"contractSigned\":\"no\"");
        _lead.PipelineState.Should().Contain("\"contractSigned\":\"pending\"");
    }

    [Fact]
    public async Task RefusesToCountersignABodyThatNoLongerMatchesItsHash()
    {
        // BodyHashAtSend is printed to both parties as tamper-evidence. Evidence
        // nobody checks is decoration. Editing after send is refused by the state
        // machine, so this can only happen from outside the application — a support
        // script, a data fix, a partial restore.
        var draft = await DraftReadyToSendAsync();
        var sent = await _sut.SendAsync(draft.Id, UserId, OrgId, resend: false);
        await _sut.SignByTokenAsync(TokenFrom(sent.Value),
            new SignContractRequest { SignatureName = "Jean Dupont", Agreed = true }, null, null);

        // Straight at the row, the way a SQL script would.
        _store.Single(c => c.Id == draft.Id).Body += "\n\nAnd a clause nobody signed.";

        var refused = await _sut.CountersignAsync(draft.Id, UserId, OrgId,
            new SignContractRequest { SignatureName = "Kia", Agreed = true }, null);

        refused.IsFailure.Should().BeTrue();
        refused.Error.Should().Be(DomainErrors.Contract.BodyChangedSinceSend);
        _store.Single(c => c.Id == draft.Id).Status.Should().Be(ContractStatuses.SignedByClient);
        // And it is on the record, because a tamper detection nobody can see later
        // is not a detection.
        _events.Should().Contain(e => e.Type == "tamper_detected");
    }

    [Fact]
    public async Task TheDtoReportsWhetherTheBodyStillMatchesItsHash()
    {
        var draft = await DraftReadyToSendAsync();
        // Never sent: nothing to disagree with yet.
        (await _sut.GetAsync(draft.Id, OrgId)).Value.BodyMatchesHashAtSend.Should().BeTrue();

        await _sut.SendAsync(draft.Id, UserId, OrgId, resend: false);
        (await _sut.GetAsync(draft.Id, OrgId)).Value.BodyMatchesHashAtSend.Should().BeTrue();

        _store.Single(c => c.Id == draft.Id).Body += " tampered";
        (await _sut.GetAsync(draft.Id, OrgId)).Value.BodyMatchesHashAtSend.Should().BeFalse();
    }

    [Fact]
    public async Task EverySignatureTimestampGoesOutMarkedAsUtc()
    {
        // The columns are plain datetime2 with no converter, so SQL Server hands
        // them back with Kind=Unspecified — and System.Text.Json serialises
        // Unspecified WITHOUT a trailing Z, so the browser read every signature
        // time as local. Correct on the response that minted the value, wrong on
        // every later read of the same contract.
        var draft = await DraftReadyToSendAsync();
        var sent = await _sut.SendAsync(draft.Id, UserId, OrgId, resend: false);
        await _sut.SignByTokenAsync(TokenFrom(sent.Value),
            new SignContractRequest { SignatureName = "Jean Dupont", Agreed = true }, null, null);

        // Simulate the round trip: the row comes back from SQL Server with no Kind.
        var row = _store.Single(c => c.Id == draft.Id);
        row.CreatedAtUtc = DateTime.SpecifyKind(row.CreatedAtUtc, DateTimeKind.Unspecified);
        row.SentAtUtc = DateTime.SpecifyKind(row.SentAtUtc!.Value, DateTimeKind.Unspecified);
        row.ClientSignedAtUtc = DateTime.SpecifyKind(row.ClientSignedAtUtc!.Value, DateTimeKind.Unspecified);

        var dto = (await _sut.GetAsync(draft.Id, OrgId)).Value;
        dto.CreatedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        dto.SentAtUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
        dto.ClientSignedAtUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
        dto.Events.Should().OnlyContain(e => e.AtUtc.Kind == DateTimeKind.Utc);
    }

    [Fact]
    public async Task TheDtoCarriesWhenTheSigningLinkExpires()
    {
        // So the CRM can tell "waiting on them" apart from "they can no longer open
        // it" — it used to report the counterparty as the hold-up for a month after
        // their link had died.
        var draft = await DraftReadyToSendAsync();
        await _sut.SendAsync(draft.Id, UserId, OrgId, resend: false);

        var dto = (await _sut.GetAsync(draft.Id, OrgId)).Value;
        dto.SigningLinkExpiresAtUtc.Should().NotBeNull();
        dto.SigningLinkExpiresAtUtc!.Value.Should().BeAfter(DateTime.UtcNow.AddDays(29));
        dto.SigningLinkExpiresAtUtc.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task TheSignerIsToldWhetherTheirCopyActuallyWentOut()
    {
        // The signing page used to state as fact that a copy had been emailed and
        // instruct them to keep it as their record — on a deployment where the send
        // had failed and nothing had arrived.
        _email.Setup(e => e.SendExecutedContractEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<EmailAttachment?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var draft = await DraftReadyToSendAsync();
        var sent = await _sut.SendAsync(draft.Id, UserId, OrgId, resend: false);
        var token = TokenFrom(sent.Value);
        await _sut.SignByTokenAsync(token,
            new SignContractRequest { SignatureName = "Jean Dupont", Agreed = true }, null, null);
        await _sut.CountersignAsync(draft.Id, UserId, OrgId,
            new SignContractRequest { SignatureName = "Kia", Agreed = true }, null);

        var seen = await _sut.GetByTokenAsync(token, null, null);
        seen.Value.CounterSignatureName.Should().NotBeNull("it IS executed");
        seen.Value.ExecutedCopySentAtUtc.Should().BeNull("but nothing was emailed");
    }

    [Fact]
    public async Task ALostRaceIsRefusedRatherThanForcedThrough()
    {
        // Every gate in this service is a read, a state-machine check and a write,
        // with each request holding its own DbContext. Two concurrent signs both
        // read `sent`, both passed, and both wrote — the row kept the SECOND
        // signer's name over the first's. "Cannot sign twice" was advice.
        var draft = await DraftReadyToSendAsync();
        var sent = await _sut.SendAsync(draft.Id, UserId, OrgId, resend: false);
        var token = TokenFrom(sent.Value);

        // The row moved underneath us, which is what the concurrency token reports.
        _contracts.Setup(r => r.UpdateAsync(It.IsAny<Contract>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var refused = await _sut.SignByTokenAsync(token,
            new SignContractRequest { SignatureName = "Jean Dupont", Agreed = true }, null, null);
        refused.IsFailure.Should().BeTrue();
        refused.Error.Should().Be(DomainErrors.Contract.ChangedByAnother);

        var voidRefused = await _sut.VoidAsync(draft.Id, UserId, OrgId, "kill it");
        voidRefused.IsFailure.Should().BeTrue();
        voidRefused.Error.Should().Be(DomainErrors.Contract.ChangedByAnother);
    }

    [Fact]
    public async Task ReadingAContractNeverFailsForALostRace()
    {
        // The one place a lost race is ignored: stamping "they opened it". Failing
        // to record that must never stop the counterparty reading the contract.
        var draft = await DraftReadyToSendAsync();
        var sent = await _sut.SendAsync(draft.Id, UserId, OrgId, resend: false);
        _contracts.Setup(r => r.UpdateAsync(It.IsAny<Contract>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var read = await _sut.GetByTokenAsync(TokenFrom(sent.Value), "1.1.1.1", "UA");
        read.IsSuccess.Should().BeTrue();
        read.Value.CanSign.Should().BeTrue();
    }

    [Fact]
    public async Task ALeadlessContractLinksSomewhereReal()
    {
        // CreateDraftAsync supports a deal-only contract, and interpolating a null
        // Guid produced "https://crm.example.com/leads/" — the leads LIST, so the
        // one link in a "they have signed, come and countersign" email went nowhere.
        var deal = Guid.NewGuid();
        var draft = await _sut.CreateDraftAsync(UserId, OrgId, new CreateContractDraftRequest
        {
            DealId = deal,
            TemplateOverride = "A short agreement with no placeholders at all.",
            Title = "Sponsorship",
        });
        await _sut.UpdateDraftAsync(draft.Value.Id, UserId, OrgId, new UpdateContractRequest
        {
            CounterpartyName = "Jean Dupont", CounterpartyEmail = "jean@example.com",
        });
        var sent = await _sut.SendAsync(draft.Value.Id, UserId, OrgId, resend: false);
        await _sut.SignByTokenAsync(TokenFrom(sent.Value),
            new SignContractRequest { SignatureName = "Jean Dupont", Agreed = true }, null, null);

        _email.Verify(e => e.SendContractSignedNotificationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            $"https://crm.example.com/deals/{deal}", It.IsAny<CancellationToken>()), Times.Once);
    }

}
