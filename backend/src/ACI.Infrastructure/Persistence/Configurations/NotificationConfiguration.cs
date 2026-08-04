using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Type).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Title).HasMaxLength(512).IsRequired();
        builder.Property(e => e.Message).HasColumnType("nvarchar(max)");
        builder.Property(e => e.Link).HasMaxLength(512);
        builder.Property(e => e.SourceKey).HasMaxLength(256);

        // Cascade on the recipient: a deleted user's alerts have no other owner.
        builder.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
        builder.HasOne(e => e.Organization).WithMany().HasForeignKey(e => e.OrganizationId).IsRequired(false);

        // The bell's two hot queries are "newest for this user in this workspace"
        // and "how many of those are unread", so both are covered by one index.
        builder.HasIndex(e => new { e.UserId, e.OrganizationId, e.Read });
        builder.HasIndex(e => e.CreatedAtUtc);

        // A generator can re-run safely: the same SourceKey cannot be inserted
        // twice for one recipient. Filtered so the many rows with no SourceKey
        // (hand-created notifications) are not forced to be distinct.
        builder.HasIndex(e => new { e.UserId, e.SourceKey })
            .IsUnique()
            .HasFilter("[SourceKey] IS NOT NULL");
    }
}
