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
