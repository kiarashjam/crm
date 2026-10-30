using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.ImportFiles;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Importing contacts and companies, which had no import at all.
/// </summary>
public class ContactCompanyImportTests
{
    private static async Task<HttpClient> WorkspaceAsync(CustomWebApplicationFactory factory)
    {
        var (client, userId) = await SignInAsync(factory, "owner@example.ch");
        client.In(await CreateAsync(factory, userId, "Pavillon46"));
        return client;
    }

    [Fact]
    public async Task ContactsAreMatchedByEmailAndLinkedToTheirCompanyByName()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = await WorkspaceAsync(factory);
        var acme = (await OkJson(await client.PostAsync("/api/companies", Body(new { name = "Acme SA" })), "add a company")).GetProperty("id").GetGuid();
        await OkJson(await client.PostAsync("/api/contacts", Body(new { name = "Jean Dupont", email = "jean@dupont.example" })), "add a contact");

        var csv = "Name;E-Mail;Firma;Do not contact\n"
            + "Jean Dupont;JEAN@dupont.example;;\n"
            + "Marc Dubois;marc@dubois.example;ACME SA;\n"
            + "Lea Dubois;lea@dubois.example;Dubois Vins;ja\n";
        var analysis = await OkJson(await Analyze(client, "contact", csv), "analyze");
        var columns = analysis.GetProperty("columns").EnumerateArray().Select(c => c.GetProperty("field").GetString()).ToArray();
        columns.Should().Equal("name", "email", "company", "doNotContact");

        var result = await OkJson(await Import(client, "contact", csv, columns), "import");

        Action(result, 2).Should().Be("skip");
        Message(result, 2).Should().Contain("Already in your contacts");
        Action(result, 3).Should().Be("create");
        Notes(result, 3).Should().BeEmpty("Acme SA is already here, whatever the letter case");
        Notes(result, 4).Should().Contain("Adds the company “Dubois Vins”");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var batchId = result.GetProperty("batchId").GetGuid();
        var marc = await db.Contacts.SingleAsync(c => c.Email == "marc@dubois.example");
        marc.CompanyId.Should().Be(acme);
        marc.ImportBatchId.Should().Be(batchId);
        var lea = await db.Contacts.SingleAsync(c => c.Email == "lea@dubois.example");
        lea.DoNotContact.Should().BeTrue();
        (await db.Companies.SingleAsync(c => c.Id == lea.CompanyId)).ImportBatchId.Should().Be(batchId);
        (await db.Leads.CountAsync()).Should().Be(0, "a contact import makes contacts, not leads");
    }

    [Fact]
    public async Task ANoInTheFileNeverClearsDoNotContact()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = await WorkspaceAsync(factory);
        await OkJson(await client.PostAsync("/api/contacts", Body(new { name = "Jean Dupont", email = "jean@dupont.example", doNotContact = true })), "add a contact");

        var csv = "email,phone,do not contact\njean@dupont.example,079 123 45 67,no\n";
        var result = await OkJson(await Import(client, "contact", csv, new[] { "email", "phone", "doNotContact" }, duplicates: "update"), "import");

        Changes(result, 2).Should().Equal("Phone: empty → “079 123 45 67”");
        var jean = await factory.Services.Read(db => db.Contacts.Single());
        jean.DoNotContact.Should().BeTrue("someone who asked not to be contacted stays that way, whatever an old spreadsheet says");
        jean.Phone.Should().Be("079 123 45 67");
    }

    [Fact]
    public async Task CompaniesAreMatchedByNameOrByTheirDomain()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = await WorkspaceAsync(factory);
        await OkJson(await client.PostAsync("/api/companies", Body(new { name = "Acme SA", domain = "acme.ch" })), "add a company");
        await OkJson(await client.PostAsync("/api/companies", Body(new { name = "Beta AG" })), "add a company");

        var csv = "Company,Website,Industry,Domain\n"
            + "Acme Holding,https://www.acme.ch/about,Hospitality,\n"   // 2: Acme by its domain
            + "beta ag,,Wine,\n"                                       // 3: Beta by name
            + "Gamma GmbH,,Events,gamma.example\n"                     // 4: new
            + "Delta,,,not a domain\n";                                // 5: refused
        var columns = new[] { "name", "website", "industry", "domain" };

        var skip = await OkJson(await Preview(client, "company", csv, columns), "preview");
        Message(skip, 2).Should().Be("Already in your companies: Acme SA (same domain)");
        Message(skip, 3).Should().Be("Already in your companies: Beta AG (same name)");
        Action(skip, 4).Should().Be("create");
        Action(skip, 5).Should().Be("error");
        Message(skip, 5).Should().Be("“not a domain” is not a domain");

        var update = await OkJson(await Import(client, "company", csv, columns, duplicates: "update"), "import");
        Action(update, 2).Should().Be("update");
        Changes(update, 2).Should().Contain("Company name: “Acme SA” → “Acme Holding”");

        var companies = await factory.Services.Read(db => db.Companies.ToDictionary(c => c.Name, c => (c.Industry, c.Domain, c.ImportBatchId)));
        companies.Keys.Should().BeEquivalentTo("Acme Holding", "Beta AG", "Gamma GmbH");
        companies["Beta AG"].Industry.Should().Be("Wine");
        companies["Gamma GmbH"].Domain.Should().Be("gamma.example");
        companies["Gamma GmbH"].ImportBatchId.Should().Be(update.GetProperty("batchId").GetGuid());
        companies["Beta AG"].ImportBatchId.Should().BeNull("updated, not created, by the import");
    }
}
