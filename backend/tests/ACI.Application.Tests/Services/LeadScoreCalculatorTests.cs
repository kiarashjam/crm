using ACI.Application.Services;
using ACI.Domain.Entities;

namespace ACI.Application.Tests.Services;

public class LeadScoreCalculatorTests
{
    [Fact]
    public void Score_UsesBuiltinRules_WhenTheOrgHasNone()
    {
        var lead = new Lead
        {
            Email = "a@acme.com",
            Source = "Referral",
            Phone = "123",
            JobTitle = "Buyer",
            CompanyId = Guid.NewGuid(),
            Status = "Qualified",
            LastContactedAt = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            EmailOpenCount = 2,
        };

        var score = LeadScoreCalculator.Score(lead, Array.Empty<LeadScoringRule>());

        Assert.Equal(100, score);
    }

    [Fact]
    public void TryCompanyFromEmail_SkipsFreeInboxes()
    {
        Assert.False(LeadDomainEnricher.TryCompanyFromEmail("a@gmail.com", out _, out _));
        Assert.True(LeadDomainEnricher.TryCompanyFromEmail("a@acme.com", out var domain, out var name));
        Assert.Equal("acme.com", domain);
        Assert.Equal("Acme", name);
    }
}
