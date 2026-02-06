using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class LeadStatusConfiguration : IEntityTypeConfiguration<LeadStatus>
{
    public void Configure(EntityTypeBuilder<LeadStatus> builder)
    {
        builder.ToTable("LeadStatuses");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(128).IsRequired();
        builder.HasOne(e => e.Organization).WithMany(o => o.LeadStatuses).HasForeignKey(e => e.OrganizationId);
        builder.HasIndex(e => e.OrganizationId);
    }
}
