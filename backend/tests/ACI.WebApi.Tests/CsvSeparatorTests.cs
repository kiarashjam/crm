using System.Net;
using System.Text;
using System.Text.Json;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// A workspace's CSV exports split columns by a semicolon when it chooses, with the UTF-8 byte
/// order mark kept: Excel set up for Switzerland splits a CSV it opens on semicolons, so a
/// comma file opened there is one long column. Comma stays the default, as exports always were.
/// </summary>
public class CsvSeparatorTests
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res, string what)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{what}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<string[]> CsvLines(HttpClient client, string path)
    {
        var res = await client.GetAsync(path);
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        var bytes = await res.Content.ReadAsByteArrayAsync();
        bytes.Take(3).Should().Equal(new byte[] { 0xEF, 0xBB, 0xBF }, "Excel reads the file as UTF-8 only with its byte order mark");
        return Encoding.UTF8.GetString(bytes).TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
    }

    [Fact]
    public async Task AWorkspaceThatChoosesSemicolons_GetsThemInEveryExport_AndCommasUntilThen()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@pavillon46.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon 46");
        owner.In(org);
        await Ok(await owner.PostAsync("/api/contacts", Body(new { name = "Müller; Söhne", email = "info@mueller.ch" })), "add a contact");
        await Ok(await owner.PostAsync("/api/companies", Body(new { name = "Hotel Bellevue" })), "add a company");

        var before = await CsvLines(owner, "/api/contacts/export");
        before[0].Should().StartWith("Name,Email,Phone,Job title,Company", "comma until the workspace says otherwise");
        before.Should().Contain(l => l.StartsWith("Müller; Söhne,"), "a semicolon is no separator in a comma file");
        (await Ok(await owner.GetAsync($"/api/organizations/{org}"), "read the workspace")).GetProperty("csvSemicolon").GetBoolean().Should().BeFalse();

        var saved = await Ok(await owner.PutAsync($"/api/organizations/{org}/settings", Body(new { csvSemicolon = true })), "choose semicolons");
        saved.GetProperty("csvSemicolon").GetBoolean().Should().BeTrue();

        var contacts = await CsvLines(owner, "/api/contacts/export");
        contacts[0].Should().StartWith("Name;Email;Phone;Job title;Company;Do not contact;Archived");
        contacts.Should().Contain(l => l.StartsWith("\"Müller; Söhne\";info@mueller.ch;"), "a cell holding the separator is quoted");

        var companies = await CsvLines(owner, "/api/companies/export");
        companies[0].Should().StartWith("Name;Domain;Industry;Size;Website;Location;Contacts;Deals;Deal value");
        companies.Should().Contain(l => l.StartsWith("Hotel Bellevue;"));

        var report = await CsvLines(owner, "/api/club-reports/export");
        report[0].Should().Be("Figure;Period;Currency;Detail;Amount;Count");

        // Back to commas: nothing else about the files changes.
        await Ok(await owner.PutAsync($"/api/organizations/{org}/settings", Body(new { csvSemicolon = false })), "back to commas");
        (await CsvLines(owner, "/api/contacts/export"))[0].Should().StartWith("Name,Email,Phone");
    }

    [Fact]
    public async Task OnlyThoseWhoManageTheWorkspacesSettingsChooseTheSeparator()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@pavillon46.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon 46");
        owner.In(org);
        var (rep, repId) = await SignInAsync(factory, "rep@pavillon46.ch");
        await AddMemberAsync(factory, org, repId, OrgMemberRole.Member);
        rep.In(org);

        (await rep.PutAsync($"/api/organizations/{org}/settings", Body(new { csvSemicolon = true }))).StatusCode
            .Should().Be(HttpStatusCode.Forbidden, "a rep does not change the workspace's settings");
        (await Ok(await owner.GetAsync($"/api/organizations/{org}"), "read it")).GetProperty("csvSemicolon").GetBoolean().Should().BeFalse();
    }
}
