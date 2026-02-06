using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ToTable("Contacts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Phone).HasMaxLength(64);
        builder.Property(e => e.JobTitle).HasMaxLength(256);
        builder.Property(e => e.PreferredContactMethod).HasMaxLength(32);

        // Relationships
        builder.HasOne(e => e.User).WithMany(u => u.Contacts).HasForeignKey(e => e.UserId);
        builder.HasOne(e => e.Organization).WithMany().HasForeignKey(e => e.OrganizationId).IsRequired(false);
        builder.HasOne(e => e.Company).WithMany(c => c.Contacts).HasForeignKey(e => e.CompanyId);
        builder.HasOne(e => e.UpdatedByUser).WithMany().HasForeignKey(e => e.UpdatedByUserId).IsRequired(false);
        builder.HasOne(e => e.ArchivedByUser).WithMany().HasForeignKey(e => e.ArchivedByUserId).IsRequired(false);
        builder.HasOne(e => e.ConvertedFromLead).WithOne(l => l.ConvertedToContact).HasForeignKey<Contact>(e => e.ConvertedFromLeadId).IsRequired(false);

        // Indexes
        builder.HasIndex(e => e.OrganizationId);
        builder.HasIndex(e => e.IsArchived);
        builder.HasIndex(e => e.DoNotContact);
        // Unique email per organization (allows duplicates across different orgs)
        builder.HasIndex(e => new { e.Email, e.OrganizationId }).IsUnique().HasFilter("[IsArchived] = 0");
    }
}
