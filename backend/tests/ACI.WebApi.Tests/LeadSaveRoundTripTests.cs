using System.Net;
using System.Text.Json;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// What a lead edit changes is what ends up in the database.
/// </summary>
/// <remarks>
/// The repository copied a hand-kept list of fields onto the stored lead and dropped
/// everything else. The service tests mock the repository, so none of them saw it:
/// a score set by hand was rescored over, a first response never stopped the SLA
/// clock, job title and the other profile fields never saved, a bulk owner change
/// reported success and changed nothing, and a converted lead lost its links to the
/// contact and deal it became. These go through the real API and read the row back.
/// </remarks>
public class LeadSaveRoundTripTests
{
    private static async Task<Guid> CreateLead(HttpClient client, string email)
    {
        var res = await client.PostAsync("/api/leads", Body(new { name = "Jean Muster", email, source = "Website", status = "New" }));
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue(body);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<ACI.Domain.Entities.Lead> Stored(CustomWebApplicationFactory factory, Guid id)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Leads.AsNoTracking().SingleAsync(l => l.Id == id);
    }

    [Fact]
    public async Task AnEditSavesEveryFieldItChanges()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, userId) = await SignInAsync(factory, "rep@example.ch");
        var org = await CreateAsync(factory, userId, "Club Nautique");
        var id = await CreateLead(client.In(org), "jean@example.ch");

        var res = await client.In(org).PutAsync($"/api/leads/{id}", Body(new
        {
            leadScore = 77, status = "Contacted", jobTitle = "CFO", country = "CH", industry = "Banking", companySize = "50-200",
        }));
        res.IsSuccessStatusCode.Should().BeTrue(await res.Content.ReadAsStringAsync());

        var lead = await Stored(factory, id);
        lead.LeadScore.Should().Be(77);
        lead.ScoreIsManual.Should().BeTrue("a score set by hand must survive the next automatic rescore");
        lead.FirstResponseAtUtc.Should().NotBeNull("leaving New is a first response, so the SLA alert must stop");
        lead.JobTitle.Should().Be("CFO");
        lead.Country.Should().Be("CH");
        lead.Industry.Should().Be("Banking");
        lead.CompanySize.Should().Be("50-200");
    }

    [Fact]
    public async Task BulkSetOwnerReallyChangesTheOwner()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Club Nautique");
        var (_, repId) = await SignInAsync(factory, "rep@example.ch");
        await AddMemberAsync(factory, org, repId, OrgMemberRole.Member);
        var a = await CreateLead(owner.In(org), "a@example.ch");
        var b = await CreateLead(owner.In(org), "b@example.ch");

        var res = await owner.In(org).PostAsync("/api/lead-ops/bulk", Body(new { ids = new[] { a, b }, assignedToUserId = repId }));
        res.IsSuccessStatusCode.Should().BeTrue(await res.Content.ReadAsStringAsync());

        (await Stored(factory, a)).AssignedToUserId.Should().Be(repId);
        (await Stored(factory, b)).AssignedToUserId.Should().Be(repId);
    }

    [Fact]
    public async Task BulkSetOwnerRefusesSomeoneOutsideTheWorkspace()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Club Nautique");
        var (_, strangerId) = await SignInAsync(factory, "stranger@example.ch");
        var lead = await CreateLead(owner.In(org), "a@example.ch");

        var res = await owner.In(org).PostAsync("/api/lead-ops/bulk", Body(new { ids = new[] { lead }, assignedToUserId = strangerId }));

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Stored(factory, lead)).AssignedToUserId.Should().NotBe(strangerId);
    }

    [Fact]
    public async Task AConvertedLeadKeepsItsLinksToTheContactAndDeal()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, userId) = await SignInAsync(factory, "rep@example.ch");
        var org = await CreateAsync(factory, userId, "Club Nautique");
        var id = await CreateLead(client.In(org), "jean@example.ch");

        var res = await client.In(org).PostAsync($"/api/leads/{id}/convert", Body(new { createContact = true, createDeal = true, dealName = "Membership", dealValue = "1200" }));
        res.IsSuccessStatusCode.Should().BeTrue(await res.Content.ReadAsStringAsync());

        var lead = await Stored(factory, id);
        lead.IsConverted.Should().BeTrue();
        lead.ConvertedToContactId.Should().NotBeNull();
        lead.ConvertedToDealId.Should().NotBeNull();
    }

    [Fact]
    public async Task ALeadCannotBeConvertedIntoAnotherWorkspacesDeal()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, userId) = await SignInAsync(factory, "rep@example.ch");
        var org = await CreateAsync(factory, userId, "Club Nautique");
        var (stranger, strangerId) = await SignInAsync(factory, "stranger@example.ch");
        var otherOrg = await CreateAsync(factory, strangerId, "Other club");
        var foreignDeal = await stranger.In(otherOrg).PostAsync("/api/deals", Body(new { name = "Their deal", value = "5000" }));
        foreignDeal.IsSuccessStatusCode.Should().BeTrue(await foreignDeal.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await foreignDeal.Content.ReadAsStringAsync());
        var foreignDealId = doc.RootElement.GetProperty("id").GetGuid();
        var id = await CreateLead(client.In(org), "jean@example.ch");

        var res = await client.In(org).PostAsync($"/api/leads/{id}/convert",
            Body(new { createContact = true, createDeal = true, existingDealId = foreignDealId }));

        res.IsSuccessStatusCode.Should().BeFalse();
        var lead = await Stored(factory, id);
        lead.IsConverted.Should().BeFalse();
        lead.ConvertedToDealId.Should().BeNull();
    }
}

