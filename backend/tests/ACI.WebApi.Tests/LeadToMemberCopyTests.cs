using System.Net;
using System.Text.Json;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// A lead's tags and custom field values follow them into membership: copied to the club member
/// made from the lead (with the welcome, by an approved application, by a countersigned contract)
/// to member fields with the same key and a compatible type, never over a value the member has,
/// and once per member.
/// </summary>
public class LeadToMemberCopyTests
{
    private sealed record Club(CustomWebApplicationFactory App, HttpClient Owner, Guid OwnerId, Guid Org) : IDisposable
    {
        public void Dispose() => App.Dispose();
    }

    private static async Task<Club> ClubAsync()
    {
        var app = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(app, "owner@pavillon46.ch");
        var org = await CreateAsync(app, ownerId, "Pavillon 46");
        return new Club(app, owner.In(org), ownerId, org);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res, string step, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(expected, $"{step} should answer {(int)expected}, got {(int)res.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task SaveAsync(Club club, params object[] rows)
    {
        using var scope = club.App.Services.CreateScope();
        await using var db = scope.ServiceProvider.EveryWorkspace();
        db.AddRange(rows);
        await db.SaveChangesAsync();
    }

    private static CustomFieldDefinition Field(Club club, string entity, string key, string type, params string[] options) => new()
    {
        Id = Guid.NewGuid(), OrganizationId = club.Org, OwnerUserId = club.OwnerId, EntityType = entity, Key = key, Label = key, Type = type,
        OptionsJson = options.Length == 0 ? null : JsonSerializer.Serialize(options), CreatedAtUtc = DateTime.UtcNow,
    };

    private static CustomFieldEntry Value(Club club, CustomFieldDefinition field, Guid record, Action<CustomFieldEntry> set)
    {
        var entry = new CustomFieldEntry
        {
            Id = Guid.NewGuid(), OrganizationId = club.Org, OwnerUserId = club.OwnerId, EntityType = field.EntityType, RecordId = record,
            FieldId = field.Id, UpdatedAtUtc = DateTime.UtcNow,
        };
        set(entry);
        return entry;
    }

    private static CustomFieldEntry Text(Club club, CustomFieldDefinition field, Guid record, string text) =>
        Value(club, field, record, e => { e.TextValue = text; e.TextKey = text; });

    private static Tag NewTag(Club club, string name) => new()
    {
        Id = Guid.NewGuid(), OrganizationId = club.Org, OwnerUserId = club.OwnerId, Name = name, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow,
    };

    private static RecordTag On(Club club, Tag tag, string entity, Guid record) => new()
    {
        Id = Guid.NewGuid(), OrganizationId = club.Org, OwnerUserId = club.OwnerId, TagId = tag.Id, EntityType = entity, RecordId = record, CreatedAtUtc = DateTime.UtcNow,
    };

    /// <summary>The lead, created over the API, with an "Interests" multiselect and a "Wine lover" tag the member side shares.</summary>
    private static async Task<(Guid Lead, CustomFieldDefinition MemberInterests, Tag WineLover)> LeadWithInterestsAsync(Club club, string name = "Anna Muster", string email = "anna@example.ch")
    {
        var lead = (await Json(await club.Owner.PostAsync("/api/leads", Body(new { name, email, status = "New" })), "add the lead")).GetProperty("id").GetGuid();
        var leadInterests = Field(club, RecordTypes.Lead, "interests", CustomFieldTypes.MultiSelect, "Wine", "Golf", "Sailing");
        var memberInterests = Field(club, RecordTypes.Member, "interests", CustomFieldTypes.MultiSelect, "Wine", "Golf", "Art");
        var wine = NewTag(club, "Wine lover");
        await SaveAsync(club, leadInterests, memberInterests, wine,
            Text(club, leadInterests, lead, "Wine"), Text(club, leadInterests, lead, "Sailing"),
            On(club, wine, RecordTypes.Lead, lead));
        return (lead, memberInterests, wine);
    }

    /// <summary>What the member carries now: their tags and the options of their "Interests".</summary>
    private static Task<(List<Guid> Tags, List<string> Interests)> MemberDetailsAsync(Club club, Guid memberId, Guid interestsField) =>
        club.App.Services.Read(db => (
            db.RecordTags.Where(t => t.EntityType == RecordTypes.Member && t.RecordId == memberId).Select(t => t.TagId).ToList(),
            db.CustomFieldEntries.Where(e => e.RecordId == memberId && e.FieldId == interestsField).Select(e => e.TextValue!).OrderBy(t => t).ToList()));

    [Fact]
    public async Task ConvertingTheLeadWithTheWelcome_CarriesItsTagsAndInterestsOntoTheMember()
    {
        using var club = await ClubAsync();
        var (lead, interests, wine) = await LeadWithInterestsAsync(club);
        var converted = await Json(await club.Owner.PostAsync($"/api/leads/{lead}/convert",
            Body(new { createContact = true, createDeal = true, dealName = "Founding membership Anna", dealAmount = 4800m })), "convert");
        await Json(await club.Owner.PutAsync("/api/onboarding/settings", Body(new { webhookUrl = "https://portal.example.com/api/integrations/cadence/onboarding", defaultLanguage = "en" })), "set the portal up");
        await Json(await club.Owner.PostAsync("/api/onboarding/settings/secret", null), "its secret");

        await Json(await club.Owner.PostAsync($"/api/deals/{converted.GetProperty("dealId").GetGuid()}/onboarding",
            Body(new { leadId = lead, startUndetermined = true, language = "en", addToClubMembers = true })), "convert with the welcome", HttpStatusCode.Created);

        var member = await club.App.Services.Read(db => db.ClubMembers.Single());
        member.LeadDetailsCopiedAtUtc.Should().NotBeNull();
        var (tags, values) = await MemberDetailsAsync(club, member.Id, interests.Id);
        tags.Should().Equal(wine.Id);
        values.Should().Equal(new[] { "Wine" }, "only the options the member field has: it has no Sailing");
    }

    [Fact]
    public async Task ApprovingTheLeadsApplication_CarriesItsTagsAndInterestsOntoTheMember()
    {
        using var club = await ClubAsync();
        var (lead, interests, wine) = await LeadWithInterestsAsync(club);
        var application = await Json(await club.Owner.PostAsync($"/api/applications/from-lead/{lead}", null), "apply from the lead");
        await Json(await club.Owner.PutAsync($"/api/applications/{application.GetProperty("id").GetGuid()}", Body(new { requestedTier = "Silver" })), "pick the tier");

        var approved = await Json(await club.Owner.PostAsync($"/api/applications/{application.GetProperty("id").GetGuid()}/approve", Body(new { })), "approve");

        var memberId = approved.GetProperty("memberId").GetGuid();
        var (tags, values) = await MemberDetailsAsync(club, memberId, interests.Id);
        tags.Should().Equal(wine.Id);
        values.Should().Equal("Wine");
        // Approving again (a double click, a second reviewer) copies nothing twice.
        await Json(await club.Owner.PostAsync($"/api/applications/{application.GetProperty("id").GetGuid()}/approve", Body(new { })), "approve again");
        (await MemberDetailsAsync(club, memberId, interests.Id)).Should().BeEquivalentTo((tags, values));
    }

    [Fact]
    public async Task CountersigningTheLeadsMembershipContract_CarriesItsTagsAndInterestsOntoTheMember()
    {
        using var club = await ClubAsync();
        var (lead, interests, wine) = await LeadWithInterestsAsync(club, "Jean-Michel Dupont", "jm@example.ch");
        var application = await Json(await club.Owner.PostAsync($"/api/applications/from-lead/{lead}", null), "apply from the lead");
        await Json(await club.Owner.PutAsync($"/api/applications/{application.GetProperty("id").GetGuid()}", Body(new { requestedTier = "Silver" })), "pick the tier");
        var draft = await Json(await club.Owner.PostAsync("/api/contracts/draft", Body(new { leadId = lead, title = "Membership Agreement" })), "draft");
        var contractId = draft.GetProperty("id").GetString();
        await Json(await club.Owner.PutAsync($"/api/contracts/{contractId}", Body(new
        {
            title = "Membership Agreement", body = "MEMBERSHIP AGREEMENT\n\n1. Fees: CHF 129 a month.\n",
            counterpartyName = "Jean-Michel Dupont", counterpartyEmail = "jm@example.ch",
        })), "edit");
        var token = (await Json(await club.Owner.PostAsync($"/api/contracts/{contractId}/send", null), "send")).GetProperty("signingUrl").GetString()!.Split("/sign/").Last();
        using (var anon = club.App.CreateClient())
            await Json(await anon.PostAsync($"/api/public/contracts/{token}/sign", Body(new { signatureName = "Jean-Michel Dupont", agreed = true })), "sign");

        await Json(await club.Owner.PostAsync($"/api/contracts/{contractId}/countersign", Body(new { signatureName = "Club Secretary", agreed = true })), "countersign");

        var member = await club.App.Services.Read(db => db.ClubMembers.Single());
        member.LeadId.Should().Be(lead);
        var (tags, values) = await MemberDetailsAsync(club, member.Id, interests.Id);
        tags.Should().Equal(wine.Id);
        values.Should().Equal("Wine");
    }

    [Fact]
    public async Task ValuesGoToCompatibleFieldsOnly_NeverOverWhatTheMemberHas_AndOncePerMember()
    {
        using var club = await ClubAsync();
        var lead = new Lead { Id = Guid.NewGuid(), UserId = club.OwnerId, OrganizationId = club.Org, Name = "Anna Muster", Email = "anna@example.ch", Status = "New", CreatedAtUtc = DateTime.UtcNow };
        var member = new ClubMember
        {
            Id = Guid.NewGuid(), OrganizationId = club.Org, OwnerUserId = club.OwnerId, FirstName = "Anna", LastName = "Muster", Email = "anna@example.ch",
            Status = "Active", JoinedAtUtc = DateTime.UtcNow, RenewsAtUtc = DateTime.UtcNow.AddYears(1), LeadId = lead.Id,
        };
        // Lead field → member field with the same key.
        var interestsL = Field(club, RecordTypes.Lead, "interests", CustomFieldTypes.MultiSelect, "Wine", "Golf", "Sailing");
        var interestsM = Field(club, RecordTypes.Member, "interests", CustomFieldTypes.MultiSelect, "wine", "Golf", "Art");
        var budgetL = Field(club, RecordTypes.Lead, "budget", CustomFieldTypes.Number);
        var budgetM = Field(club, RecordTypes.Member, "budget", CustomFieldTypes.Text);
        var tierL = Field(club, RecordTypes.Lead, "tier_wish", CustomFieldTypes.Select, "Silver", "Gold");
        var tierM = Field(club, RecordTypes.Member, "tier_wish", CustomFieldTypes.Select, "Silver", "Gold");
        var sportL = Field(club, RecordTypes.Lead, "sport", CustomFieldTypes.Select, "Tennis", "Padel");
        var sportM = Field(club, RecordTypes.Member, "sport", CustomFieldTypes.MultiSelect, "Tennis", "Golf");
        var dietL = Field(club, RecordTypes.Lead, "diet", CustomFieldTypes.Select, "Vegan", "None");
        var dietM = Field(club, RecordTypes.Member, "diet", CustomFieldTypes.Select, "Vegetarian", "None");
        var noteL = Field(club, RecordTypes.Lead, "note", CustomFieldTypes.Text);
        var noteM = Field(club, RecordTypes.Member, "note", CustomFieldTypes.Text);
        var birthdayL = Field(club, RecordTypes.Lead, "birthday", CustomFieldTypes.Date);
        var birthdayM = Field(club, RecordTypes.Member, "birthday", CustomFieldTypes.Date);
        var depositL = Field(club, RecordTypes.Lead, "pledge", CustomFieldTypes.Money);
        var depositM = Field(club, RecordTypes.Member, "pledge", CustomFieldTypes.Money);
        var leadOnly = Field(club, RecordTypes.Lead, "campaign_code", CustomFieldTypes.Text);
        var vip = NewTag(club, "VIP");
        var wine = NewTag(club, "Wine lover");
        await SaveAsync(club, lead, member, interestsL, interestsM, budgetL, budgetM, tierL, tierM, sportL, sportM, dietL, dietM,
            noteL, noteM, birthdayL, birthdayM, depositL, depositM, leadOnly, vip, wine,
            Text(club, interestsL, lead.Id, "Wine"), Text(club, interestsL, lead.Id, "Golf"), Text(club, interestsL, lead.Id, "Sailing"),
            Value(club, budgetL, lead.Id, e => e.NumberValue = 50000m),
            Text(club, tierL, lead.Id, "Gold"),
            Text(club, sportL, lead.Id, "Tennis"),
            Text(club, dietL, lead.Id, "Vegan"),
            Text(club, noteL, lead.Id, "Met at the summer party"),
            Text(club, noteM, member.Id, "Already here"),
            Value(club, birthdayL, lead.Id, e => e.DateValue = new DateOnly(1980, 5, 17)),
            Value(club, depositL, lead.Id, e => { e.NumberValue = 2500m; e.Currency = "EUR"; }),
            Text(club, leadOnly, lead.Id, "SUMMER26"),
            On(club, vip, RecordTypes.Lead, lead.Id), On(club, wine, RecordTypes.Lead, lead.Id), On(club, vip, RecordTypes.Member, member.Id));

        async Task<LeadToMemberCopyResult> CopyAsync(Guid? from = null)
        {
            using var scope = club.App.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ILeadToMemberCopy>().CopyAsync(club.Org, member.Id, from, club.OwnerId);
        }

        var result = await CopyAsync();

        result.Should().Be(new LeadToMemberCopyResult(true, 1, 5), "the Wine lover tag; interests, tier, sport, birthday and pledge");
        var values = await club.App.Services.Read(db => db.CustomFieldEntries.Where(e => e.RecordId == member.Id).ToList());
        values.Where(v => v.FieldId == interestsM.Id).Select(v => v.TextValue).Should().BeEquivalentTo(new[] { "wine", "Golf" },
            "only the member field's options, in its own spelling: Sailing is not one");
        values.Single(v => v.FieldId == tierM.Id).TextValue.Should().Be("Gold");
        values.Single(v => v.FieldId == sportM.Id).TextValue.Should().Be("Tennis", "a single choice fits a field of several");
        values.Single(v => v.FieldId == birthdayM.Id).DateValue.Should().Be(new DateOnly(1980, 5, 17));
        var pledge = values.Single(v => v.FieldId == depositM.Id);
        (pledge.NumberValue, pledge.Currency).Should().Be((2500m, "EUR"));
        values.Single(v => v.FieldId == noteM.Id).TextValue.Should().Be("Already here", "a value the member has is never overwritten");
        values.Should().NotContain(v => v.FieldId == budgetM.Id, "a number does not go into a text field");
        values.Should().NotContain(v => v.FieldId == dietM.Id, "Vegan is not one of the member field's options");
        values.Should().OnlyContain(v => v.EntityType == RecordTypes.Member && v.OrganizationId == club.Org);
        var tags = await club.App.Services.Read(db => db.RecordTags.Where(t => t.RecordId == member.Id).Select(t => t.TagId).ToList());
        tags.Should().BeEquivalentTo(new[] { vip.Id, wine.Id }, "VIP once, though both carried it");

        // Staff take the Wine lover tag off; a second way of making the member (or a retry) puts nothing back.
        using (var scope = club.App.Services.CreateScope())
        {
            await using var db = scope.ServiceProvider.EveryWorkspace();
            db.RecordTags.RemoveRange(db.RecordTags.Where(t => t.RecordId == member.Id && t.TagId == wine.Id));
            await db.SaveChangesAsync();
        }
        (await CopyAsync()).Should().Be(LeadToMemberCopyResult.None);
        (await club.App.Services.Read(db => db.RecordTags.Count(t => t.RecordId == member.Id))).Should().Be(1);
        (await club.App.Services.Read(db => db.CustomFieldEntries.Count(e => e.RecordId == member.Id))).Should().Be(values.Count);
    }

    [Fact]
    public async Task ALeadFromAnotherWorkspaceIsNeverCopiedFrom()
    {
        using var club = await ClubAsync();
        var (_, otherOwner) = await SignInAsync(club.App, "owner@cercle.example");
        var otherOrg = await CreateAsync(club.App, otherOwner, "Cercle du Lac");
        var theirLead = new Lead { Id = Guid.NewGuid(), UserId = otherOwner, OrganizationId = otherOrg, Name = "Paul", Email = "paul@example.ch", Status = "New", CreatedAtUtc = DateTime.UtcNow };
        var theirTag = new Tag { Id = Guid.NewGuid(), OrganizationId = otherOrg, OwnerUserId = otherOwner, Name = "Secret", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var member = new ClubMember
        {
            Id = Guid.NewGuid(), OrganizationId = club.Org, OwnerUserId = club.OwnerId, FirstName = "Anna", LastName = "Muster", Email = "anna@example.ch",
            Status = "Active", JoinedAtUtc = DateTime.UtcNow, RenewsAtUtc = DateTime.UtcNow.AddYears(1),
        };
        await SaveAsync(club, theirLead, theirTag, member,
            new RecordTag { Id = Guid.NewGuid(), OrganizationId = otherOrg, OwnerUserId = otherOwner, TagId = theirTag.Id, EntityType = RecordTypes.Lead, RecordId = theirLead.Id, CreatedAtUtc = DateTime.UtcNow });

        using var scope = club.App.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ILeadToMemberCopy>().CopyAsync(club.Org, member.Id, theirLead.Id, club.OwnerId);

        result.Should().Be(LeadToMemberCopyResult.None);
        (await club.App.Services.Read(db => db.RecordTags.Count(t => t.RecordId == member.Id))).Should().Be(0);
        (await club.App.Services.Read(db => db.ClubMembers.Single(m => m.Id == member.Id).LeadDetailsCopiedAtUtc)).Should().BeNull("nothing was copied, so a later way still may");
    }
}
