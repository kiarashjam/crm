using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("Organizations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(256).IsRequired();
        builder.HasOne(e => e.OwnerUser).WithMany().HasForeignKey(e => e.OwnerUserId);
        builder.HasIndex(e => e.OwnerUserId);
        builder.Property(e => e.WebhookPassword).HasMaxLength(256);
        builder.Property(e => e.SendingAddress).HasMaxLength(256);
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired().HasDefaultValue("CHF");
        builder.Property(e => e.Locale).HasMaxLength(35).IsRequired().HasDefaultValue("de-CH");
        builder.Property(e => e.TimeZone).HasMaxLength(64).IsRequired().HasDefaultValue("Europe/Zurich");
    }
}
