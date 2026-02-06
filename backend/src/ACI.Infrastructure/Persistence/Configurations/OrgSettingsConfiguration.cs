using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class OrgSettingsConfiguration : IEntityTypeConfiguration<OrgSettings>
{
    public void Configure(EntityTypeBuilder<OrgSettings> builder)
    {
        builder.ToTable("OrgSettings");
        builder.HasKey(e => e.OrganizationId);
        builder.Property(e => e.CompanyName).HasMaxLength(256).IsRequired();
        builder.Property(e => e.BrandTone).HasConversion<string>().HasMaxLength(32);
        builder.HasOne(e => e.Organization).WithOne(o => o.Settings).HasForeignKey<OrgSettings>(e => e.OrganizationId);
    }
}
