using System.Text.Json;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// A lead's details carried onto its member on a real SQL Server: the member marked by one
/// statement inside the copy's transaction, so ways of making the member that run at the same
/// moment copy the tags and values once.
/// </summary>
public class LeadToMemberCopyOnSqlServerTests
{
    private static async Task<JsonElement> Success(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"{(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    [SqlServerFact]
    public async Task ThreeWaysAtOnceCopyTheLeadsTagsAndValuesOnce()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (_, ownerId) = await SignInAsync(app, $"owner-{Guid.NewGuid():N}@pavillon46.ch");
            var org = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var lead = new Lead { Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, Name = "Anna Muster", Email = "anna@example.ch", Status = "New", CreatedAtUtc = now };
            var member = new ClubMember
            {
                Id = Guid.NewGuid(), OrganizationId = org, OwnerUserId = ownerId, FirstName = "Anna", LastName = "Muster", Email = "anna@example.ch",
                Status = "Active", JoinedAtUtc = now, RenewsAtUtc = now.AddYears(1), LeadId = lead.Id,
            };
            CustomFieldDefinition Field(string entity) => new()
            {
                Id = Guid.NewGuid(), OrganizationId = org, OwnerUserId = ownerId, EntityType = entity, Key = "interests", Label = "Interests",
                Type = CustomFieldTypes.MultiSelect, OptionsJson = JsonSerializer.Serialize(new[] { "Wine", "Golf" }), CreatedAtUtc = now,
            };
            var leadField = Field(RecordTypes.Lead);
            var memberField = Field(RecordTypes.Member);
            CustomFieldEntry Interest(string option) => new()
            {
                Id = Guid.NewGuid(), OrganizationId = org, OwnerUserId = ownerId, EntityType = RecordTypes.Lead, RecordId = lead.Id, FieldId = leadField.Id,
                TextValue = option, TextKey = option, UpdatedAtUtc = now,
            };
            var tag = new Tag { Id = Guid.NewGuid(), OrganizationId = org, OwnerUserId = ownerId, Name = "Wine lover", CreatedAtUtc = now, UpdatedAtUtc = now };
            await HealthWorld.SaveAsync(app,
                new Organization { Id = org, Name = "Pavillon 46", OwnerUserId = ownerId, CreatedAtUtc = now },
                new OrganizationMember { OrganizationId = org, UserId = ownerId, Role = OrgMemberRole.Owner, JoinedAtUtc = now },
                lead, member, leadField, memberField, tag, Interest("Wine"), Interest("Golf"),
                new RecordTag { Id = Guid.NewGuid(), OrganizationId = org, OwnerUserId = ownerId, TagId = tag.Id, EntityType = RecordTypes.Lead, RecordId = lead.Id, CreatedAtUtc = now });

            async Task<LeadToMemberCopyResult> CopyAsync()
            {
                using var scope = app.Services.CreateScope();
                return await scope.ServiceProvider.GetRequiredService<ILeadToMemberCopy>().CopyAsync(org, member.Id, null, ownerId);
            }
            var results = await Task.WhenAll(CopyAsync(), CopyAsync(), CopyAsync());

            results.Count(r => r.Copied).Should().Be(1, "the member is marked once");
            results.Single(r => r.Copied).Should().Be(new LeadToMemberCopyResult(true, 1, 1));
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [CustomFieldEntries] WHERE [RecordId] = '{member.Id}'")).Should().Be(2, "Wine and Golf, once each");
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [RecordTags] WHERE [RecordId] = '{member.Id}'")).Should().Be(1);
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [ClubMembers] WHERE [Id] = '{member.Id}' AND [LeadDetailsCopiedAtUtc] IS NOT NULL")).Should().Be(1);
            (await CopyAsync()).Should().Be(LeadToMemberCopyResult.None);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task ApprovingTheLeadsApplication_CopiesOnceTheApprovalHasCommitted()
    {
        // The approval runs in a transaction of its own and the copy in another, after it commits:
        // only a real database shows the copy starts its own rather than leaning on the finished one.
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (client, ownerId) = await SignInAsync(app, $"owner-{Guid.NewGuid():N}@pavillon46.ch");
            var org = (await Success(await client.PostAsync("/api/organizations", Body(new { name = "Pavillon 46" })))).GetProperty("id").GetGuid();
            client.In(org);
            var lead = (await Success(await client.PostAsync("/api/leads", Body(new { name = "Anna Muster", email = "anna@example.ch", status = "New" }))))
                .GetProperty("id").GetGuid();
            var now = DateTime.UtcNow;
            var tag = new Tag { Id = Guid.NewGuid(), OrganizationId = org, OwnerUserId = ownerId, Name = "Wine lover", CreatedAtUtc = now, UpdatedAtUtc = now };
            await HealthWorld.SaveAsync(app, tag,
                new RecordTag { Id = Guid.NewGuid(), OrganizationId = org, OwnerUserId = ownerId, TagId = tag.Id, EntityType = RecordTypes.Lead, RecordId = lead, CreatedAtUtc = now });
            var application = (await Success(await client.PostAsync($"/api/applications/from-lead/{lead}", null))).GetProperty("id").GetGuid();
            await Success(await client.PutAsync($"/api/applications/{application}", Body(new { requestedTier = "Gold" })));

            var approved = await Success(await client.PostAsync($"/api/applications/{application}/approve", Body(new { })));

            var member = approved.GetProperty("memberId").GetGuid();
            approved.GetProperty("approved").GetBoolean().Should().BeTrue();
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [RecordTags] WHERE [RecordId] = '{member}' AND [TagId] = '{tag.Id}'"))
                .Should().Be(1, "the lead's tag is on the member admitted");
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [ClubMembers] WHERE [Id] = '{member}' AND [LeadDetailsCopiedAtUtc] IS NOT NULL")).Should().Be(1);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
