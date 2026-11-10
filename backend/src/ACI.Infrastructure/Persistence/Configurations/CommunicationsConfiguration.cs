using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

// Member communications and consent. Made by migrations, like every table since the baseline.

internal sealed class ConsentRecordConfiguration : IEntityTypeConfiguration<ConsentRecord>
{
    public void Configure(EntityTypeBuilder<ConsentRecord> builder)
    {
        builder.ToTable("ConsentRecords");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.SubjectType).HasMaxLength(16).IsRequired();
        builder.Property(e => e.Channel).HasMaxLength(16).IsRequired();
        builder.Property(e => e.Purpose).HasMaxLength(16).IsRequired();
        builder.Property(e => e.Source).HasMaxLength(16).IsRequired();
        builder.Property(e => e.Address).HasMaxLength(256);
        builder.Property(e => e.Detail).HasMaxLength(500);

        // One answer stands per person, channel and purpose. Two staff recording at once
        // both mark the old one superseded and add their own; the second add is refused here.
        builder.HasIndex(e => new { e.OrganizationId, e.SubjectType, e.SubjectId, e.Channel, e.Purpose })
            .IsUnique().HasFilter("[IsCurrent] = 1")
            .HasDatabaseName("IX_ConsentRecords_Current");
        // A person's history, newest first; and everything about one person, for their data export or erasure.
        builder.HasIndex(e => new { e.OrganizationId, e.SubjectType, e.SubjectId, e.RecordedAtUtc })
            .HasDatabaseName("IX_ConsentRecords_Subject");
        // An unsubscribe link, an export or an erasure that knows the person only by their address.
        builder.HasIndex(e => new { e.OrganizationId, e.Address })
            .HasDatabaseName("IX_ConsentRecords_Address");
    }
}

internal sealed class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable("Campaigns");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Channel).HasMaxLength(16).IsRequired();
        builder.Property(e => e.Category).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Purpose).HasMaxLength(16).IsRequired();
        builder.Property(e => e.Subject).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Body).IsRequired();
        builder.Property(e => e.Segment).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(16).IsRequired();
        builder.Property(e => e.Version).IsConcurrencyToken();

        // A workspace's campaigns, newest first.
        builder.HasIndex(e => new { e.OrganizationId, e.CreatedAtUtc }).HasDatabaseName("IX_Campaigns_OrganizationId_CreatedAtUtc");
        // The scheduler's question, every half minute: which campaigns are due, or stalled?
        builder.HasIndex(e => new { e.Status, e.ScheduledAtUtc }).HasDatabaseName("IX_Campaigns_Status_ScheduledAtUtc");
    }
}

internal sealed class MemberMessageConfiguration : IEntityTypeConfiguration<MemberMessage>
{
    public void Configure(EntityTypeBuilder<MemberMessage> builder)
    {
        builder.ToTable("MemberMessages");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.MemberName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(256);
        builder.Property(e => e.CampaignName).HasMaxLength(200);
        builder.Property(e => e.Channel).HasMaxLength(16).IsRequired();
        builder.Property(e => e.Category).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Purpose).HasMaxLength(16);
        builder.Property(e => e.Subject).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Preview).HasMaxLength(500);
        builder.Property(e => e.Status).HasMaxLength(16).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500);

        // One message per member per campaign: a sending taken up again after it died, or
        // two senders at once, cannot write a second, and so cannot email a member twice.
        builder.HasIndex(e => new { e.CampaignId, e.MemberId }).IsUnique().HasFilter("[CampaignId] IS NOT NULL")
            .HasDatabaseName("IX_MemberMessages_CampaignId_MemberId");
        // The communications log, newest first, for the club and for one member.
        builder.HasIndex(e => new { e.OrganizationId, e.CreatedAtUtc }).HasDatabaseName("IX_MemberMessages_OrganizationId_CreatedAtUtc");
        builder.HasIndex(e => new { e.OrganizationId, e.MemberId, e.CreatedAtUtc }).HasDatabaseName("IX_MemberMessages_OrganizationId_MemberId_CreatedAtUtc");
        builder.HasIndex(e => e.OutboundMessageId).HasDatabaseName("IX_MemberMessages_OutboundMessageId");
    }
}
