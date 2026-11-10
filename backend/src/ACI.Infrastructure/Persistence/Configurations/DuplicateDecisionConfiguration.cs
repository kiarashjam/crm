using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class DuplicateDecisionConfiguration : IEntityTypeConfiguration<DuplicateDecision>
{
    public void Configure(EntityTypeBuilder<DuplicateDecision> builder)
    {
        builder.ToTable("DuplicateDecisions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Kind).HasMaxLength(16).IsRequired();
        builder.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(e => e.Organization).WithMany().HasForeignKey(e => e.OrganizationId).IsRequired(false);

        // A workspace's decisions are read by kind; a personal workspace's by its user.
        builder.HasIndex(e => new { e.OrganizationId, e.Kind }).HasDatabaseName("IX_DuplicateDecisions_OrganizationId_Kind");
        builder.HasIndex(e => e.UserId).HasDatabaseName("IX_DuplicateDecisions_UserId");
    }
}
