using System.IO.Compression;
using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// A person's custom field values and tags are theirs: in their export, in the erasure's
/// preview, and gone after it, while the workspace's fields and tags stay for everyone else.
/// </summary>
public class FlexDataPrivacyTests
{
    private const string AnnaEmail = "anna.keller@example.ch";

    private sealed record Flex(Guid DietField, Guid VipTag, Guid BobEntry, Guid BobTag);

    private static async Task<Flex> SeedFlexAsync(CustomWebApplicationFactory factory, PersonDataTests.Seeded s)
    {
        await using var db = factory.Services.EveryWorkspace();
        var now = DateTime.UtcNow;
        db.AppliesFieldDefaults = false;
        var diet = new CustomFieldDefinition
        {
            Id = Guid.NewGuid(), OrganizationId = s.Org, OwnerUserId = s.Owner, EntityType = RecordTypes.Member, Key = "diet", Label = "Diet",
            Type = CustomFieldTypes.Text, CreatedAtUtc = now,
        };
        var source = new CustomFieldDefinition
        {
            Id = Guid.NewGuid(), OrganizationId = s.Org, OwnerUserId = s.Owner, EntityType = RecordTypes.Contact, Key = "met_at", Label = "Met at",
            Type = CustomFieldTypes.Text, CreatedAtUtc = now,
        };
        var vip = new Tag { Id = Guid.NewGuid(), OrganizationId = s.Org, OwnerUserId = s.Owner, Name = "VIP", CreatedAtUtc = now, UpdatedAtUtc = now };
        CustomFieldEntry Entry(CustomFieldDefinition field, Guid record, string text) => new()
        {
            Id = Guid.NewGuid(), OrganizationId = s.Org, OwnerUserId = s.Owner, EntityType = field.EntityType, RecordId = record, FieldId = field.Id,
            TextValue = text, TextKey = text, UpdatedAtUtc = now,
        };
        RecordTag Tagged(string type, Guid record) => new()
        {
            Id = Guid.NewGuid(), OrganizationId = s.Org, OwnerUserId = s.Owner, TagId = vip.Id, EntityType = type, RecordId = record, CreatedAtUtc = now,
        };
        var bobEntry = Entry(diet, s.BobMemberId, "Vegetarian");
        var bobTag = Tagged(RecordTypes.Member, s.BobMemberId);
        db.AddRange(diet, source, vip,
            Entry(diet, s.MemberId, "Allergic to nuts"), Entry(source, s.ContactId, "The spring gala"), bobEntry,
            Tagged(RecordTypes.Member, s.MemberId), Tagged(RecordTypes.Lead, s.LeadId), bobTag);
        await db.SaveChangesAsync();
        return new Flex(diet.Id, vip.Id, bobEntry.Id, bobTag.Id);
    }

    private static object Anna(PersonDataTests.Seeded s, bool confirm = false) => confirm
        ? new
        {
            leadIds = new[] { s.LeadId }, contactIds = new[] { s.ContactId }, memberIds = new[] { s.MemberId }, applicationIds = new[] { s.ApplicationId },
            addresses = new[] { AnnaEmail }, confirm = "ERASE", reference = "Letter of 12 November",
        }
        : new
        {
            leadIds = new[] { s.LeadId }, contactIds = new[] { s.ContactId }, memberIds = new[] { s.MemberId }, applicationIds = new[] { s.ApplicationId },
            addresses = new[] { AnnaEmail },
        };

    [Fact]
    public async Task TheExportHoldsTheirCustomValuesAndTags_AndNobodyElses()
    {
        var (factory, client, s) = await PersonDataTests.ClubAsync();
        using var _ = factory;
        await SeedFlexAsync(factory, s);

        var res = await client.PostAsync("/api/privacy/export", Body(Anna(s)));
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        using var zip = new ZipArchive(await res.Content.ReadAsStreamAsync(), ZipArchiveMode.Read);
        string Read(string path)
        {
            var entry = zip.GetEntry(path);
            entry.Should().NotBeNull($"{path} should be in the export");
            using var reader = new StreamReader(entry!.Open());
            return reader.ReadToEnd();
        }

        var values = JsonDocument.Parse(Read("records/custom-fields.json")).RootElement.GetProperty("rows");
        values.EnumerateArray().Select(v => (v.GetProperty("field").GetString(), v.GetProperty("value").GetString()))
            .Should().BeEquivalentTo(new[] { ("Diet", "Allergic to nuts"), ("Met at", "The spring gala") });
        var tags = JsonDocument.Parse(Read("records/tags.json")).RootElement.GetProperty("rows");
        tags.EnumerateArray().Select(t => (t.GetProperty("record").GetString(), t.GetProperty("tag").GetString()))
            .Should().BeEquivalentTo(new[] { ("member", "VIP"), ("lead", "VIP") });
        Read("records/custom-fields.json").Should().NotContain("Vegetarian", "Bob's diet is his");
    }

    [Fact]
    public async Task AnErasureTakesTheirValuesAndTagsOffEveryRecord_ThePseudonymisedMemberToo()
    {
        var (factory, client, s) = await PersonDataTests.ClubAsync();
        using var _ = factory;
        var flex = await SeedFlexAsync(factory, s);

        var plan = await PersonDataTestsOk(await client.PostAsync("/api/privacy/erasure/preview", Body(Anna(s))), "preview");
        var lines = plan.GetProperty("lines").EnumerateArray().ToDictionary(l => l.GetProperty("area").GetString()!, l => (l.GetProperty("action").GetString(), l.GetProperty("count").GetInt32()));
        lines["Custom field values on their records"].Should().Be(("delete", 2));
        lines["Tags on their records"].Should().Be(("delete", 2));

        await PersonDataTestsOk(await client.PostAsync("/api/privacy/erasure", Body(Anna(s, confirm: true))), "erase");

        await using var db = factory.Services.EveryWorkspace();
        (await db.ClubMembers.AnyAsync(m => m.Id == s.MemberId)).Should().BeTrue("the member row stays for the books");
        (await db.CustomFieldEntries.AnyAsync(e => e.RecordId == s.MemberId || e.RecordId == s.ContactId)).Should().BeFalse();
        (await db.RecordTags.AnyAsync(t => t.RecordId == s.MemberId || t.RecordId == s.LeadId)).Should().BeFalse();
        (await db.CustomFieldEntries.AnyAsync(e => e.Id == flex.BobEntry)).Should().BeTrue("Bob's value is his");
        (await db.RecordTags.AnyAsync(t => t.Id == flex.BobTag)).Should().BeTrue();
        (await db.Tags.AnyAsync(t => t.Id == flex.VipTag)).Should().BeTrue("the tag stays for the workspace's other records");
        (await db.CustomFieldDefinitions.AnyAsync(f => f.Id == flex.DietField)).Should().BeTrue();
    }

    private static async Task<JsonElement> PersonDataTestsOk(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }
}
