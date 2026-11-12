using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class ForecastSnapshotConfiguration : IEntityTypeConfiguration<ForecastSnapshot>
{
    public void Configure(EntityTypeBuilder<ForecastSnapshot> builder)
    {
        builder.ToTable("ForecastSnapshots");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Currency).HasMaxLength(8).IsRequired();
        builder.Property(e => e.AssigneeName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.WeightedValue).HasPrecision(18, 2);
        builder.Property(e => e.OpenValue).HasPrecision(18, 2);
        builder.Property(e => e.OverdueValue).HasPrecision(18, 2);
        builder.HasIndex(e => new { e.OrganizationId, e.MonthUtc });
    }
}

internal sealed class SentMessageConfiguration : IEntityTypeConfiguration<SentMessage>
{
    public void Configure(EntityTypeBuilder<SentMessage> builder)
    {
        builder.ToTable("SentMessages");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.To).HasMaxLength(2000).IsRequired();
        builder.Property(e => e.Cc).HasMaxLength(2000);
        builder.Property(e => e.Bcc).HasMaxLength(2000);
        builder.Property(e => e.Subject).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Body).IsRequired();
        builder.HasIndex(e => new { e.OrganizationId, e.SentAtUtc });
    }
}

internal sealed class AutomationRuleConfiguration : IEntityTypeConfiguration<AutomationRule>
{
    public void Configure(EntityTypeBuilder<AutomationRule> builder)
    {
        builder.ToTable("AutomationRules");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Trigger).HasMaxLength(40).IsRequired();
        builder.Property(e => e.ActionsJson).IsRequired();
        // Checked on every save of the rule and moved only by an edit (AutomationService),
        // never by the run count.
        builder.Property(e => e.Version).IsConcurrencyToken();
        builder.HasIndex(e => new { e.OrganizationId, e.Trigger });
    }
}

internal sealed class AutomationRunConfiguration : IEntityTypeConfiguration<AutomationRun>
{
    public void Configure(EntityTypeBuilder<AutomationRun> builder)
    {
        builder.ToTable("AutomationRuns");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.RuleName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Trigger).HasMaxLength(40).IsRequired();
        builder.Property(e => e.SourceKey).HasMaxLength(80).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.RecordType).HasMaxLength(20).IsRequired();
        builder.Property(e => e.RecordName).HasMaxLength(300);
        builder.Property(e => e.Email).HasMaxLength(320);
        builder.Property(e => e.StepsJson).IsRequired();
        builder.Property(e => e.CurrentPath).HasMaxLength(40).IsRequired();
        builder.Property(e => e.WaitingFor).HasMaxLength(300);
        builder.Property(e => e.LastError).HasMaxLength(1000);
        // A runner's save lands only while the run still holds the claim it took.
        builder.Property(e => e.ClaimCount).IsConcurrencyToken();
        builder.HasOne<AutomationRule>().WithMany().HasForeignKey(e => e.RuleId).OnDelete(DeleteBehavior.Cascade);
        // What the runner looks for: runs not finished, by when they are due. A finished run
        // has no due time, so the index holds only the runs still going.
        builder.HasIndex(e => e.NextAttemptAtUtc).HasFilter("[NextAttemptAtUtc] IS NOT NULL");
        builder.HasIndex(e => new { e.OrganizationId, e.RuleId, e.StartedAtUtc });
        builder.HasIndex(e => new { e.OrganizationId, e.Status, e.StartedAtUtc });
    }
}

internal sealed class AutomationRunStepConfiguration : IEntityTypeConfiguration<AutomationRunStep>
{
    public void Configure(EntityTypeBuilder<AutomationRunStep> builder)
    {
        builder.ToTable("AutomationRunSteps");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Path).HasMaxLength(40).IsRequired();
        builder.Property(e => e.StepType).HasMaxLength(40).IsRequired();
        builder.Property(e => e.Outcome).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Summary).HasMaxLength(1000).IsRequired();
        builder.Property(e => e.Error).HasMaxLength(1000);
        builder.HasOne<AutomationRun>().WithMany().HasForeignKey(e => e.RunId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.RunId, e.AtUtc });
    }
}

internal sealed class AutomationFireConfiguration : IEntityTypeConfiguration<AutomationFire>
{
    public void Configure(EntityTypeBuilder<AutomationFire> builder)
    {
        builder.ToTable("AutomationFires");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.SourceKey).HasMaxLength(80).IsRequired();
        builder.HasIndex(e => new { e.RuleId, e.SourceKey }).IsUnique();
    }
}

internal sealed class ClubMemberConfiguration : IEntityTypeConfiguration<ClubMember>
{
    public void Configure(EntityTypeBuilder<ClubMember> builder)
    {
        builder.ToTable("ClubMembers");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.LastName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Phone).HasMaxLength(64);
        builder.Property(e => e.Tier).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(32).IsRequired();
        builder.Property(e => e.DuesFrequency).HasMaxLength(32).IsRequired();
        builder.Property(e => e.DuesAmount).HasPrecision(18, 2);
        builder.Property(e => e.HouseAccountBalance).HasPrecision(18, 2);
        builder.Property(e => e.ManualBalance).HasPrecision(18, 2);
        builder.Property(e => e.StatusBeforeLapse).HasMaxLength(32);
        builder.HasIndex(e => e.OrganizationId);
        // A contact or lead page asks which member is that person.
        builder.HasIndex(e => new { e.OrganizationId, e.ContactId }).HasFilter("[ContactId] IS NOT NULL");
        builder.HasIndex(e => new { e.OrganizationId, e.LeadId }).HasFilter("[LeadId] IS NOT NULL");
    }
}

internal sealed class ClubVisitConfiguration : IEntityTypeConfiguration<ClubVisit>
{
    public void Configure(EntityTypeBuilder<ClubVisit> builder)
    {
        builder.ToTable("ClubVisits");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.MemberName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.MemberTier).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Venue).HasMaxLength(32).IsRequired();
        builder.HasIndex(e => e.MemberId);
    }
}

internal sealed class LoyaltyLedgerEntryConfiguration : IEntityTypeConfiguration<LoyaltyLedgerEntry>
{
    public void Configure(EntityTypeBuilder<LoyaltyLedgerEntry> builder)
    {
        builder.ToTable("LoyaltyLedger");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.MemberName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Kind).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(40).IsRequired();
        builder.HasIndex(e => e.MemberId);
    }
}
