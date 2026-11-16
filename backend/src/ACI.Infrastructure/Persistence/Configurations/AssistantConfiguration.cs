using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

// The assistant's tables (see Assistant.cs in ACI.Domain). The unique indexes are the
// database's own guard for what the services rely on: one usage row per workspace (or person,
// outside any organization) and month, which the conditional UPDATE that takes a call from the
// month's allowance counts on; one settings row per workspace; and one morning brief per
// workspace, person and day, taken by inserting it.

internal sealed class AiUsageConfiguration : IEntityTypeConfiguration<AiUsage>
{
    public void Configure(EntityTypeBuilder<AiUsage> builder)
    {
        builder.ToTable("AiUsage");
        builder.HasKey(e => e.Id);
        // Not filtered: every row leaves one of the two empty (an organization's has no person,
        // a person's has no organization), and EF's usual filter on nullable columns of a unique
        // index ("IS NOT NULL") would leave every row out of it. SQL Server's unique index treats
        // the empty ones as equal, which is the one row per workspace and month wanted here.
        builder.HasIndex(e => new { e.OrganizationId, e.UserId, e.Month }).IsUnique().HasFilter(null);
    }
}

internal sealed class AssistantSettingsConfiguration : IEntityTypeConfiguration<AssistantSettings>
{
    public void Configure(EntityTypeBuilder<AssistantSettings> builder)
    {
        builder.ToTable("AssistantSettings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Version).IsConcurrencyToken();
        builder.HasIndex(e => e.OrganizationId).IsUnique();
    }
}

internal sealed class AssistantBriefConfiguration : IEntityTypeConfiguration<AssistantBrief>
{
    public void Configure(EntityTypeBuilder<AssistantBrief> builder)
    {
        builder.ToTable("AssistantBriefs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Status).HasMaxLength(16).IsRequired();
        builder.HasIndex(e => new { e.OrganizationId, e.UserId, e.Day }).IsUnique();
        builder.HasIndex(e => new { e.Status, e.CreatedAtUtc });
    }
}
