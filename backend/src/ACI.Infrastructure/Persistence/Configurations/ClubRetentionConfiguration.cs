using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

// Retention and referrals (see ClubRetention.cs in ACI.Domain). The unique indexes are the
// database's own guard for what the services check first: one settings row and one risk row
// per member in a workspace, one renewal row per member, source, subscription and end, one referral bonus per
// member brought in, and one daily scoring run per workspace and day. The in-memory provider
// the HTTP tests use enforces none of them; the SQL Server tests show they hold.

internal sealed class ClubRetentionSettingsConfiguration : IEntityTypeConfiguration<ClubRetentionSettings>
{
    public void Configure(EntityTypeBuilder<ClubRetentionSettings> builder)
    {
        builder.ToTable("ClubRetentionSettings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.SaveEmailPurpose).HasMaxLength(16).IsRequired();
        builder.Property(e => e.SaveEmailSubject).HasMaxLength(200);
        builder.Property(e => e.SaveEmailBody).HasMaxLength(4000);
        builder.Property(e => e.ReferralBonus).HasMaxLength(16).IsRequired();
        builder.Property(e => e.ReferralBonusAmount).HasPrecision(18, 2);
        builder.Property(e => e.SponsorRule).HasMaxLength(16).IsRequired();
        builder.Property(e => e.UpdatedByName).HasMaxLength(200);
        builder.Property(e => e.Version).IsConcurrencyToken();
        builder.HasIndex(e => e.OrganizationId).IsUnique();
    }
}

internal sealed class ClubMemberRiskConfiguration : IEntityTypeConfiguration<ClubMemberRisk>
{
    public void Configure(EntityTypeBuilder<ClubMemberRisk> builder)
    {
        builder.ToTable("ClubMemberRisks");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Level).HasMaxLength(16).IsRequired();
        builder.Property(e => e.FactorsJson).IsRequired();
        builder.HasIndex(e => new { e.OrganizationId, e.MemberId }).IsUnique();
        // The at-risk list and the renewals board read a workspace's members by level.
        builder.HasIndex(e => new { e.OrganizationId, e.Level, e.Score });
    }
}

internal sealed class ClubRenewalConfiguration : IEntityTypeConfiguration<ClubRenewal>
{
    public void Configure(EntityTypeBuilder<ClubRenewal> builder)
    {
        builder.ToTable("ClubRenewals");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Source).HasMaxLength(16).IsRequired();
        builder.Property(e => e.Stage).HasMaxLength(16);
        builder.Property(e => e.StageSetByName).HasMaxLength(200);
        builder.Property(e => e.Note).HasMaxLength(1000);
        builder.Property(e => e.Version).IsConcurrencyToken();
        // One row per renewal: the daily run adding it and someone marking it at the same moment make one.
        // A subscription's renewal is its own: a member's dues and their locker, started the same
        // day, end at the same midnight. A membership's has no subscription, and SQL Server holds
        // a null to one row per end like any other value, so the filter EF adds by default for a
        // nullable column (which would leave memberships unguarded) is taken off.
        builder.HasIndex(e => new { e.OrganizationId, e.MemberId, e.Source, e.SubscriptionId, e.EndsAtUtc }).IsUnique().HasFilter(null);
        builder.HasIndex(e => new { e.OrganizationId, e.EndsAtUtc });
    }
}

internal sealed class ClubReferralBonusConfiguration : IEntityTypeConfiguration<ClubReferralBonus>
{
    public void Configure(EntityTypeBuilder<ClubReferralBonus> builder)
    {
        builder.ToTable("ClubReferralBonuses");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Kind).HasMaxLength(16).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(16).IsRequired();
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500);
        // Once per member brought in, whatever two approvals or a retry do.
        builder.HasIndex(e => new { e.OrganizationId, e.ReferredMemberId }).IsUnique();
        builder.HasIndex(e => new { e.OrganizationId, e.ReferrerMemberId });
    }
}

internal sealed class ClubMemberConcernConfiguration : IEntityTypeConfiguration<ClubMemberConcern>
{
    public void Configure(EntityTypeBuilder<ClubMemberConcern> builder)
    {
        builder.ToTable("ClubMemberConcerns");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Kind).HasMaxLength(16).IsRequired();
        builder.Property(e => e.Note).HasMaxLength(1000).IsRequired();
        builder.Property(e => e.RecordedByName).HasMaxLength(200);
        builder.Property(e => e.ResolvedByName).HasMaxLength(200);
        builder.Property(e => e.Resolution).HasMaxLength(1000);
        builder.HasIndex(e => new { e.OrganizationId, e.MemberId, e.RecordedAtUtc });
    }
}

internal sealed class ClubScoringRunConfiguration : IEntityTypeConfiguration<ClubScoringRun>
{
    public void Configure(EntityTypeBuilder<ClubScoringRun> builder)
    {
        builder.ToTable("ClubScoringRuns");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Trigger).HasMaxLength(16).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(16).IsRequired();
        builder.Property(e => e.Failure).HasMaxLength(2000);
        builder.HasIndex(e => new { e.OrganizationId, e.StartedAtUtc });
        // The daily run, once per workspace per local day: a second server finds the day taken.
        builder.HasIndex(e => new { e.OrganizationId, e.ScheduledFor }).IsUnique().HasFilter("[ScheduledFor] IS NOT NULL");
    }
}
