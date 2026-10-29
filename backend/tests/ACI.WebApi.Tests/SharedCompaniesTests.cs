using System.Net;
using System.Text.Json;
using ACI.Domain.Enums;
using FluentAssertions;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Everyone in a workspace can open and edit its companies; no one outside can.
/// </summary>
/// <remarks>
/// The list was scoped to the workspace, but opening or editing a company also
/// required being the person who created it. A teammate saw the company in the list
/// and got "not found" when they clicked it.
/// </remarks>
public class SharedCompaniesTests
{
    [Fact]
    public async Task ATeammateOpensAndEditsACompanySomeoneElseCreated()
    {
        using var factory = new CustomWebApplicationFactory();
        var (anna, annaId) = await SignInAsync(factory, "anna@example.ch");
        var org = await CreateAsync(factory, annaId, "Club Nautique");
        var (ben, benId) = await SignInAsync(factory, "ben@example.ch");
        await AddMemberAsync(factory, org, benId, OrgMemberRole.Member);

        var created = await anna.In(org).PostAsync("/api/companies", Body(new { name = "Banque Léman" }));
        created.IsSuccessStatusCode.Should().BeTrue(await created.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = doc.RootElement.GetProperty("id").GetGuid();

        (await ben.In(org).GetAsync($"/api/companies/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        var edit = await ben.In(org).PutAsync($"/api/companies/{id}", Body(new { name = "Banque Léman SA" }));
        edit.IsSuccessStatusCode.Should().BeTrue(await edit.Content.ReadAsStringAsync());
        (await edit.Content.ReadAsStringAsync()).Should().Contain("Banque Léman SA");
    }

    [Fact]
    public async Task SomeoneFromAnotherWorkspaceCannotOpenIt()
    {
        using var factory = new CustomWebApplicationFactory();
        var (anna, annaId) = await SignInAsync(factory, "anna@example.ch");
        var org = await CreateAsync(factory, annaId, "Club Nautique");
        var (eve, eveId) = await SignInAsync(factory, "eve@example.ch");
        var otherOrg = await CreateAsync(factory, eveId, "Other club");

        var created = await anna.In(org).PostAsync("/api/companies", Body(new { name = "Banque Léman" }));
        using var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = doc.RootElement.GetProperty("id").GetGuid();

        (await eve.In(otherOrg).GetAsync($"/api/companies/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await eve.In(otherOrg).PutAsync($"/api/companies/{id}", Body(new { name = "Mine now" }))).IsSuccessStatusCode.Should().BeFalse();
    }
}
