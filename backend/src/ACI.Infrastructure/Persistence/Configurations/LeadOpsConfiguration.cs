using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class LeadStatusEventConfiguration : IEntityTypeConfiguration<LeadStatusEvent>
{
    public void Configure(EntityTypeBuilder<LeadStatusEvent> builder)
    {
        builder.ToTable("LeadStatusEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.FromStatus).HasMaxLength(64);
        builder.Property(e => e.ToStatus).HasMaxLength(64).IsRequired();
        builder.HasIndex(e => e.LeadId);
    }
}

internal sealed class LeadScoringRuleConfiguration : IEntityTypeConfiguration<LeadScoringRule>
{
    public void Configure(EntityTypeBuilder<LeadScoringRule> builder)
    {
        builder.ToTable("LeadScoringRules");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Field).HasMaxLength(40).IsRequired();
        builder.Property(e => e.MatchValue).HasMaxLength(120);
    }
}

internal sealed class LeadAssignmentRuleConfiguration : IEntityTypeConfiguration<LeadAssignmentRule>
{
    public void Configure(EntityTypeBuilder<LeadAssignmentRule> builder)
    {
        builder.ToTable("LeadAssignmentRules");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Kind).HasMaxLength(32).IsRequired();
        builder.Property(e => e.SourceMatch).HasMaxLength(80);
        builder.Property(e => e.MemberUserIds).HasMaxLength(2000);
    }
}

internal sealed class LeadTerritoryConfiguration : IEntityTypeConfiguration<LeadTerritory>
{
    public void Configure(EntityTypeBuilder<LeadTerritory> builder)
    {
        builder.ToTable("LeadTerritories");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Countries).HasMaxLength(500);
        builder.Property(e => e.Industries).HasMaxLength(500);
    }
}

internal sealed class LeadQueueConfiguration : IEntityTypeConfiguration<LeadQueue>
{
    public void Configure(EntityTypeBuilder<LeadQueue> builder)
    {
        builder.ToTable("LeadQueues");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.MemberUserIds).HasMaxLength(2000);
    }
}

internal sealed class LeadCaptureFormConfiguration : IEntityTypeConfiguration<LeadCaptureForm>
{
    public void Configure(EntityTypeBuilder<LeadCaptureForm> builder)
    {
        builder.ToTable("LeadCaptureForms");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.PublicToken).HasMaxLength(64).IsRequired();
        builder.Property(e => e.SuccessMessage).HasMaxLength(300);
        builder.HasIndex(e => e.PublicToken).IsUnique();
    }
}

internal sealed class LeadOpsSettingsConfiguration : IEntityTypeConfiguration<LeadOpsSettings>
{
    public void Configure(EntityTypeBuilder<LeadOpsSettings> builder)
    {
        builder.ToTable("LeadOpsSettings");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.OrganizationId);
    }
}

internal sealed class OutreachSequenceConfiguration : IEntityTypeConfiguration<OutreachSequence>
{
    public void Configure(EntityTypeBuilder<OutreachSequence> builder)
    {
        builder.ToTable("OutreachSequences");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(160).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.HasMany(e => e.Steps).WithOne(s => s.Sequence!).HasForeignKey(s => s.SequenceId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class OutreachStepConfiguration : IEntityTypeConfiguration<OutreachStep>
{
    public void Configure(EntityTypeBuilder<OutreachStep> builder)
    {
        builder.ToTable("OutreachSteps");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Type).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Subject).HasMaxLength(200);
        builder.Property(e => e.Body).HasMaxLength(4000);
        builder.Property(e => e.TaskTitle).HasMaxLength(200);
        builder.Property(e => e.Note).HasMaxLength(1000);
    }
}

internal sealed class OutreachEnrollmentConfiguration : IEntityTypeConfiguration<OutreachEnrollment>
{
    public void Configure(EntityTypeBuilder<OutreachEnrollment> builder)
    {
        builder.ToTable("OutreachEnrollments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.TargetType).HasMaxLength(20).IsRequired();
        builder.Property(e => e.TargetName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.TargetEmail).HasMaxLength(256);
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.HasIndex(e => new { e.Status, e.NextActionAtUtc });
    }
}
