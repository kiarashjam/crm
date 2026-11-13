using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

// A workspace's privacy settings, its log of erasures and its retention runs (see Privacy.cs
// in ACI.Domain). The unique indexes are the database's own guard for what the services
// check first: one settings row per workspace, one erasure per number in a workspace (the
// number is the pseudonym), and one scheduled retention run per workspace and day. The
// in-memory provider the HTTP tests use enforces none of them.

internal sealed class PrivacySettingsConfiguration : IEntityTypeConfiguration<PrivacySettings>
{
    public void Configure(EntityTypeBuilder<PrivacySettings> builder)
    {
        builder.ToTable("PrivacySettings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ControllerName).HasMaxLength(200);
        builder.Property(e => e.ControllerAddress).HasMaxLength(500);
        builder.Property(e => e.ControllerEmail).HasMaxLength(256);
        builder.Property(e => e.DpoContact).HasMaxLength(300);
        builder.Property(e => e.MarketingConsent).HasMaxLength(16).IsRequired();
        builder.Property(e => e.MarketingConsentText).HasMaxLength(500);
        builder.Property(e => e.UpdatedByName).HasMaxLength(200);
        builder.Property(e => e.Version).IsConcurrencyToken();
        builder.HasIndex(e => e.OrganizationId).IsUnique();
    }
}

internal sealed class PersonErasureConfiguration : IEntityTypeConfiguration<PersonErasure>
{
    public void Configure(EntityTypeBuilder<PersonErasure> builder)
    {
        builder.ToTable("PersonErasures");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Trigger).HasMaxLength(16).IsRequired();
        builder.Property(e => e.ErasedByName).HasMaxLength(200);
        builder.Property(e => e.Reference).HasMaxLength(200);
        builder.Property(e => e.SummaryJson).IsRequired();
        // The number is the pseudonym the books carry: two erasures must never share one.
        builder.HasIndex(e => new { e.OrganizationId, e.Number }).IsUnique();
        builder.HasIndex(e => new { e.OrganizationId, e.ErasedAtUtc });
    }
}

internal sealed class RetentionRunConfiguration : IEntityTypeConfiguration<RetentionRun>
{
    public void Configure(EntityTypeBuilder<RetentionRun> builder)
    {
        builder.ToTable("RetentionRuns");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Trigger).HasMaxLength(16).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(16).IsRequired();
        builder.Property(e => e.StartedByName).HasMaxLength(200);
        builder.Property(e => e.Failures).HasMaxLength(2000);
        builder.HasIndex(e => new { e.OrganizationId, e.StartedAtUtc });
        // The daily run, once per workspace per local day: a second server finds the day taken.
        builder.HasIndex(e => new { e.OrganizationId, e.ScheduledFor }).IsUnique().HasFilter("[ScheduledFor] IS NOT NULL");
    }
}
