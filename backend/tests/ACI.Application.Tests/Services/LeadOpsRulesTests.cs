using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.Domain.Entities;

namespace ACI.Application.Tests.Services;

/// <summary>
/// Scoring, routing and enrichment rules from #57, each pinned on a case it got wrong.
/// </summary>
public class LeadOpsRulesTests
{
    // ── "qualified" in the agreed stages ─────────────────────────────────────

    [Theory]
    [InlineData("Contract Pending", true)]
    [InlineData("Awaiting Signature", true)]
    [InlineData("Signed", true)]
    [InlineData("Qualified", true)]   // retired label, mapped to Contract Pending
    [InlineData("Open Deal", true)]   // retired label, mapped to Signed
    [InlineData("Connected", false)]
    [InlineData("New", false)]
    [InlineData("Lost / Not Interested", false)] // last in the list, not furthest along
    [InlineData("Unqualified", false)]           // used to MATCH a "qualif" substring
    [InlineData("Something custom", false)]
    public void ReachedContractPending(string status, bool reached)
    {
        LeadStatusVocabulary.ReachedStage(status, "Contract Pending").Should().Be(reached);
    }

    [Fact]
    public void TheBuiltInQualifiedRuleFiresAgain()
    {
        // It was status-contains-"qualified", which no current status contains, so
        // the +20 was never awarded to anyone.
        Lead At(string status) => new() { Status = status, CreatedAtUtc = DateTime.UtcNow, LastContactedAt = DateTime.UtcNow };
        var none = Array.Empty<LeadScoringRule>();
        (LeadScoreCalculator.Score(At("Awaiting Signature"), none) - LeadScoreCalculator.Score(At("Connected"), none))
            .Should().Be(20);
        (LeadScoreCalculator.Score(At("Lost / Not Interested"), none) - LeadScoreCalculator.Score(At("Connected"), none))
            .Should().Be(0);
    }

    // ── routing pool ─────────────────────────────────────────────────────────

    [Fact]
    public void SomeoneWhoLeftIsTakenOutOfTheRotation()
    {
        // Queue [alice, bob]; bob leaves. Bob kept getting every second lead.
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var queue = new LeadQueue { Id = Guid.NewGuid(), MemberUserIds = LeadAutomation.JoinIds(new[] { alice, bob }) };
        var rule = new LeadAssignmentRule { QueueId = queue.Id, Kind = "RoundRobin" };

        LeadAutomation.Pool(rule, new[] { queue }, members: new[] { alice }).Should().Equal(alice);
    }

    [Fact]
    public void ARuleWhoseListedPeopleAllLeftFallsBackToTheWorkspace()
    {
        var gone = Guid.NewGuid();
        var here = Guid.NewGuid();
        var rule = new LeadAssignmentRule { Kind = "RoundRobin", MemberUserIds = LeadAutomation.JoinIds(new[] { gone }) };

        LeadAutomation.Pool(rule, Array.Empty<LeadQueue>(), members: new[] { here }).Should().Equal(here);
    }

    // ── company from email ───────────────────────────────────────────────────

    [Theory]
    [InlineData("jean@bluewin.ch")]
    [InlineData("anna@gmx.ch")]
    [InlineData("marc@hispeed.ch")]
    [InlineData("luca@libero.it")]
    [InlineData("sophie@orange.fr")]
    public void SwissAndNeighbouringPersonalMailIsNotACompany(string email)
    {
        // A bluewin.ch lead got a company called "Bluewin", with every other one.
        LeadDomainEnricher.TryCompanyFromEmail(email, out _, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("jane@us.ibm.com", "ibm.com", "Ibm")]          // was "Us"
    [InlineData("tom@mail.acme.co.uk", "acme.co.uk", "Acme")]
    [InlineData("kia@pavillon46.ch", "pavillon46.ch", "Pavillon46")]
    public void TheCompanyComesFromTheRegistrableDomain(string email, string domain, string name)
    {
        LeadDomainEnricher.TryCompanyFromEmail(email, out var d, out var n).Should().BeTrue();
        d.Should().Be(domain);
        n.Should().Be(name);
    }
}
