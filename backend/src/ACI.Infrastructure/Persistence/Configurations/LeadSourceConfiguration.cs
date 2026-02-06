using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class LeadSourceConfiguration : IEntityTypeConfiguration<LeadSource>
{
    public void Configure(EntityTypeBuilder<LeadSource> builder)
    {
        builder.ToTable("LeadSources");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(128).IsRequired();
        builder.HasOne(e => e.Organization).WithMany(o => o.LeadSources).HasForeignKey(e => e.OrganizationId);
        builder.HasIndex(e => e.OrganizationId);
    }
}
