using ACI.Application.Common;
using ACI.Application.Configuration;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ACI.Application.Tests.Services;

public class SkepticScratchTests
{
    private readonly Mock<IContractRepository> _contracts = new();
    private readonly Mock<ILeadRepository> _leads = new();
    private readonly Mock<IOrganizationRepository> _orgs = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IEmailSender> _email = new();
    private ContractService _sut = null!;

    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid ColleagueId = Guid.NewGuid();
    private static readonly Guid ThirdId = Guid.NewGuid();
    private static readonly Guid OrgId = Guid.NewGuid();
    private static readonly Guid LeadId = Guid.NewGuid();

    private readonly List<Contract> _store = new();
    private readonly List<ContractEvent> _events = new();

    // captured email arguments
    private readonly List<(string To, string Body, string Block)> _executed = new();
    private readonly List<(string To, string Url)> _notified = new();
    private readonly List<(string To, string Url)> _forSignature = new();

    private Lead _lead = null!;

    public SkepticScratchTests()
    {
        _contracts.Setup(r => r.AddAsync(It.IsAny<Contract>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Contract c, CancellationToken _) => { _store.Add(c); return c; });
        _contracts.Setup(r => r.UpdateAsync(It.IsAny<Contract>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
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
            .ReturnsAsync(new Organization { Id = OrgId, Name = "Club Alpin" });

        _users.Setup(r => r.GetByIdAsync(OwnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = OwnerId, Name = "Anna", Email = "anna@club.example" });
        _users.Setup(r => r.GetByIdAsync(ColleagueId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = ColleagueId, Name = "Bruno", Email = "bruno@club.example" });
        _users.Setup(r => r.GetByIdAsync(ThirdId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = ThirdId, Name = "Carla", Email = "carla@club.example" });

        _email.Setup(e => e.SendContractForSignatureEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<EmailAttachment?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string to, string _, string __, string ___, string url, EmailAttachment? ____, CancellationToken _____) =>
                { _forSignature.Add((to, url)); return true; });
        _email.Setup(e => e.SendContractSignedNotificationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string to, string _, string __, string ___, string url, CancellationToken ____) =>
                { _notified.Add((to, url)); return true; });
        _email.Setup(e => e.SendExecutedContractEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<EmailAttachment?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string to, string _, string __, string ___, string body, string block, EmailAttachment? ____, CancellationToken _____) =>
                { _executed.Add((to, body, block)); return true; });

        _lead = new Lead { Id = LeadId, Name = "Jean Dupont", Email = "jean@example.ch" };
        _leads.Setup(r => r.GetByIdAsync(LeadId, It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _lead);
        _leads.Setup(r => r.UpdateAsync(It.IsAny<Lead>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead l, Guid _, Guid? __, CancellationToken ___) => { _lead = l; return l; });

        Build("https://crm.example.com");
    }

    private void Build(string? frontendBaseUrl)
        => _sut = new ContractService(
            _contracts.Object, _leads.Object, _orgs.Object, _users.Object, _email.Object,
            Options.Create(new EmailSettings { FrontendBaseUrl = frontendBaseUrl! }),
            Mock.Of<ILogger<ContractService>>());

    private async Task<ContractDto> DraftAsync(Guid? leadId)
    {
        var r = await _sut.CreateDraftAsync(OwnerId, OrgId, new CreateContractDraftRequest
        {
            LeadId = leadId,
            DealId = leadId is null ? Guid.NewGuid() : null,
            Values = ContractTemplate.FieldsUsed(ContractTemplate.DefaultTemplate)
                .ToDictionary(f => f, f => (string?)$"[{f}]"),
        });
        r.IsSuccess.Should().BeTrue();
        return r.Value;
    }

    private static string TokenFrom(SendContractResult r) => r.SigningUrl.Split('/').Last();

    /* ---------------------------------------------------------------- finding 1 */

    [Fact]
    public async Task F1_MultilineSignatureNameInjectsLinesIntoTheSignatureRecord()
    {
        var forged =
            "Jean Dupont\n" +
            "Signed       : 2019-04-01 09:00:00 UTC\n" +
            "Document hash: 0000000000000000000000000000000000000000000000000000000000000000\n" +
            "Note         : signed subject to clause 2 being struck out";
        forged.Length.Should().BeLessThan(300, "must fit the nvarchar(300) column");

        var draft = await DraftAsync(LeadId);
        var sent = await _sut.SendAsync(draft.Id, OwnerId, OrgId, resend: false);
        var sign = await _sut.SignByTokenAsync(TokenFrom(sent.Value),
            new SignContractRequest { SignatureName = forged, Agreed = true }, "84.226.10.3", "curl/8");
        sign.IsSuccess.Should().BeTrue("IsSignatureNameValid accepts embedded newlines");

        await _sut.CountersignAsync(draft.Id, OwnerId, OrgId,
            new SignContractRequest { SignatureName = "Anna", Agreed = true }, "198.51.100.4");

        _executed.Should().HaveCount(2);
        var block = _executed[0].Block;
        block.Should().Contain("Note         : signed subject to clause 2 being struck out");
        block.Should().Contain("Signed       : 2019-04-01 09:00:00 UTC");
        // the real lines are still there too
        block.Split('\n').Count(l => l.StartsWith("Signed       :")).Should().Be(3);
        block.Split('\n').Count(l => l.StartsWith("Document hash:")).Should().Be(2);
        _executed[1].Block.Should().Be(block, "both parties get the same forged block");

        // and the audit trail
        _events.Single(e => e.Type == "signed").Detail.Should().Contain("2019-04-01");

        // and the rendered HTML email keeps them as separate lines inside <pre>
        var html = ContractEmailContent.Executed(
            "Club Alpin", "Jean Dupont", "Club Alpin", draft.Title, "body", block, false).Html;
        html.Should().Contain("Note         : signed subject to clause 2 being struck out");

        System.Console.WriteLine("=== BLOCK ===\n" + block);
    }

    /* ---------------------------------------------------------------- finding 2 */

    [Fact]
    public async Task F2_ExecutedCopyGoesToTheActingUserNotTheOwner()
    {
        var draft = await DraftAsync(LeadId);
        var sent = await _sut.SendAsync(draft.Id, OwnerId, OrgId, resend: false);
        await _sut.SignByTokenAsync(TokenFrom(sent.Value),
            new SignContractRequest { SignatureName = "Jean Dupont", Agreed = true }, null, null);

        _notified.Should().ContainSingle().Which.To.Should().Be("anna@club.example");

        // colleague countersigns
        await _sut.CountersignAsync(draft.Id, ColleagueId, OrgId,
            new SignContractRequest { SignatureName = "Bruno", Agreed = true }, null);

        _executed.Select(x => x.To).Should().BeEquivalentTo(new[] { "jean@example.ch", "bruno@club.example" });
        _store.Single().ExecutedCopySentAtUtc.Should().NotBeNull();

        // recovery button pressed by a third colleague
        _executed.Clear();
        var again = await _sut.ResendExecutedCopyAsync(draft.Id, ThirdId, OrgId);
        again.IsSuccess.Should().BeTrue();
        again.Value.Should().BeTrue();
        _executed.Select(x => x.To).Should().BeEquivalentTo(new[] { "jean@example.ch", "carla@club.example" });
    }

    /* ---------------------------------------------------------------- finding 3 */

    [Fact]
    public async Task F3_LeadlessContractNotificationLinksToTheLeadsList()
    {
        var draft = await DraftAsync(null);
        draft.LeadId.Should().BeNull();

        var upd = await _sut.UpdateDraftAsync(draft.Id, OwnerId, OrgId, new UpdateContractRequest
        {
            CounterpartyName = "Jean Dupont",
            CounterpartyEmail = "jean@example.ch",
        });
        upd.IsSuccess.Should().BeTrue();

        var sent = await _sut.SendAsync(draft.Id, OwnerId, OrgId, resend: false);
        sent.IsSuccess.Should().BeTrue();

        var sign = await _sut.SignByTokenAsync(TokenFrom(sent.Value),
            new SignContractRequest { SignatureName = "Jean Dupont", Agreed = true }, null, null);
        sign.IsSuccess.Should().BeTrue();

        _notified.Should().ContainSingle();
        System.Console.WriteLine("NOTIFY URL = [" + _notified[0].Url + "]");
        _notified[0].Url.Should().Be("https://crm.example.com/leads/");
    }

    /* ---------------------------------------------------------------- finding 4 */

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("crm.example.com")]
    public async Task F4_NonAbsoluteBaseUrlStillReportsASuccessfulSend(string? baseUrl)
    {
        Build(baseUrl);
        var draft = await DraftAsync(LeadId);
        var sent = await _sut.SendAsync(draft.Id, OwnerId, OrgId, resend: false);

        sent.IsSuccess.Should().BeTrue();
        sent.Value.EmailSent.Should().BeTrue();
        System.Console.WriteLine($"base=[{baseUrl}] url=[{sent.Value.SigningUrl}]");
        _forSignature.Should().ContainSingle();
        _events.Any(e => e.Type == "sent" && (e.Detail ?? "").Contains("Sent to jean@example.ch")).Should().BeTrue();
    }
}
