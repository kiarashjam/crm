using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

// The recap emails' tables (see RecapEmails.cs in ACI.Domain). The unique indexes are the
// database's own guard for what the service relies on: one row of frequencies per workspace and
// person, and one recap per workspace, person, frequency and period, taken by inserting it.

internal sealed class RecapSubscriptionConfiguration : IEntityTypeConfiguration<RecapSubscription>
{
    public void Configure(EntityTypeBuilder<RecapSubscription> builder)
    {
        builder.ToTable("RecapSubscriptions");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.OrganizationId, e.UserId }).IsUnique();
        builder.HasIndex(e => e.UserId);
    }
}

internal sealed class RecapSendConfiguration : IEntityTypeConfiguration<RecapSend>
{
    public void Configure(EntityTypeBuilder<RecapSend> builder)
    {
        builder.ToTable("RecapSends");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Frequency).HasMaxLength(16).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(16).IsRequired();
        builder.HasIndex(e => new { e.OrganizationId, e.UserId, e.Frequency, e.PeriodStart }).IsUnique();
        builder.HasIndex(e => new { e.Status, e.CreatedAtUtc });
    }
}
