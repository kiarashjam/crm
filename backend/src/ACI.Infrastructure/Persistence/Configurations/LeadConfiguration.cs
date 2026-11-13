using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        builder.ToTable("Leads");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Phone).HasMaxLength(64);
        builder.Property(e => e.Source).HasMaxLength(64);
        builder.Property(e => e.Status).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.LifecycleStage).HasMaxLength(64);
        builder.Property(e => e.JobTitle).HasMaxLength(200);
        builder.Property(e => e.Country).HasMaxLength(80);
        builder.Property(e => e.Industry).HasMaxLength(120);
        builder.Property(e => e.CompanySize).HasMaxLength(40);
        builder.Property(e => e.EnrichedDomain).HasMaxLength(200);
        // A person until said otherwise: every lead made before kinds were is one.
        builder.Property(e => e.Kind).HasMaxLength(20).IsRequired().HasDefaultValue(LeadKinds.Person);
        builder.Property(e => e.EstablishmentType).HasMaxLength(40);
        builder.Property(e => e.AddressLine).HasMaxLength(200);
        builder.Property(e => e.PostalCode).HasMaxLength(20);
        builder.Property(e => e.City).HasMaxLength(100);
        builder.Property(e => e.Website).HasMaxLength(300);
        builder.Property(e => e.ContactPersonName).HasMaxLength(200);
        builder.Property(e => e.ContactPersonRole).HasMaxLength(100);
        builder.Property(e => e.ContactPersonEmail).HasMaxLength(256);
        builder.Property(e => e.ContactPersonPhone).HasMaxLength(64);
        // An edit made from an older read of the lead is refused, not written over the newer one. See IVersioned.
        builder.Property(e => e.Version).IsConcurrencyToken();

        // Relationships
        builder.HasOne(e => e.User).WithMany(u => u.Leads).HasForeignKey(e => e.UserId);
        builder.HasOne(e => e.Organization).WithMany().HasForeignKey(e => e.OrganizationId).IsRequired(false);
        builder.HasOne(e => e.Company).WithMany(c => c.Leads).HasForeignKey(e => e.CompanyId);
        builder.HasOne(e => e.LeadSource).WithMany().HasForeignKey(e => e.LeadSourceId).IsRequired(false);
        builder.HasOne(e => e.LeadStatus).WithMany().HasForeignKey(e => e.LeadStatusId).IsRequired(false);
        builder.HasOne(e => e.UpdatedByUser).WithMany().HasForeignKey(e => e.UpdatedByUserId).IsRequired(false);
        builder.HasOne(e => e.AssignedToUser).WithMany().HasForeignKey(e => e.AssignedToUserId).IsRequired(false);
        // ConvertedToContact is configured in ContactConfiguration (one-to-one)
        builder.HasOne(e => e.ConvertedToDeal).WithMany().HasForeignKey(e => e.ConvertedToDealId).IsRequired(false);
        // No cascade: deleting a contact clears the referral on its leads (ContactRepository.DeleteAsync).
        builder.HasOne(e => e.ReferredByContact).WithMany().HasForeignKey(e => e.ReferredByContactId)
            .IsRequired(false).OnDelete(DeleteBehavior.ClientSetNull);

        // Indexes
        builder.HasIndex(e => e.OrganizationId);
        builder.HasIndex(e => e.IsConverted);
        builder.HasIndex(e => e.AssignedToUserId);
        builder.HasIndex(e => e.ReferredByContactId);
        builder.HasIndex(e => new { e.OrganizationId, e.CreatedAtUtc });
        builder.HasIndex(e => new { e.OrganizationId, e.Status });
        // Undoing an import finds the records it created by this.
        builder.HasIndex(e => e.ImportBatchId);
    }
}
