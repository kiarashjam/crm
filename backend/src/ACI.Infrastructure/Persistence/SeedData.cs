using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Persistence;

public static class SeedData
{
    /// <summary>
    /// Seeded demo user. Use these credentials to sign in.
    /// </summary>
    public const string SeedUserEmail = "demo@aci.local";
    public const string SeedUserName = "Demo User";
    public const string SeedUserPassword = "DemoPass123!";

    public static async Task SeedAsync(AppDbContext db, IPasswordHasher passwordHasher, CancellationToken ct = default)
    {
        if (!await db.Users.AnyAsync(ct))
        {
            var seedUser = new User
            {
                Id = Guid.NewGuid(),
                Name = SeedUserName,
                Email = SeedUserEmail,
                PasswordHash = passwordHasher.Hash(SeedUserPassword),
                CreatedAtUtc = DateTime.UtcNow,
            };
            db.Users.Add(seedUser);
        }

        if (await db.Templates.AnyAsync(ct))
        {
            await db.SaveChangesAsync(ct);
            return;
        }

        var templates = new[]
        {
            new Template { Id = Guid.NewGuid(), Title = "First contact email", Description = "Initial outreach to new prospects", Category = "Sales", CopyTypeId = CopyTypeId.SalesEmail, Goal = "Schedule a meeting", UseCount = 245, UserId = null, IsSystemTemplate = true, CreatedAtUtc = DateTime.UtcNow },
            new Template { Id = Guid.NewGuid(), Title = "Follow-up after meeting", Description = "Thank you and next steps", Category = "Follow-up", CopyTypeId = CopyTypeId.FollowUp, Goal = "Follow up after demo", UseCount = 189, UserId = null, IsSystemTemplate = true, CreatedAtUtc = DateTime.UtcNow },
            new Template { Id = Guid.NewGuid(), Title = "Demo reminder", Description = "Confirm upcoming product demo", Category = "Meetings", CopyTypeId = CopyTypeId.SalesEmail, Goal = "Schedule a meeting", UseCount = 156, UserId = null, IsSystemTemplate = true, CreatedAtUtc = DateTime.UtcNow },
            new Template { Id = Guid.NewGuid(), Title = "Closing deal message", Description = "Final push to close the sale", Category = "Sales", CopyTypeId = CopyTypeId.DealMessage, Goal = "Close the deal", UseCount = 134, UserId = null, IsSystemTemplate = true, CreatedAtUtc = DateTime.UtcNow },
            new Template { Id = Guid.NewGuid(), Title = "Re-engagement email", Description = "Reconnect with cold leads", Category = "Re-engagement", CopyTypeId = CopyTypeId.SalesEmail, Goal = "Check in on progress", UseCount = 98, UserId = null, IsSystemTemplate = true, CreatedAtUtc = DateTime.UtcNow },
        };

        db.Templates.AddRange(templates);
        await db.SaveChangesAsync(ct);
    }
}
