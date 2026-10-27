using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class ContactRelationshipConfiguration : IEntityTypeConfiguration<ContactRelationship>
{
    public void Configure(EntityTypeBuilder<ContactRelationship> builder)
    {
        builder.ToTable("ContactRelationships");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Type).HasMaxLength(32).IsRequired();

        // No cascade: two paths from Contacts would be refused by SQL Server, so deleting
        // a contact removes its relationships first (ContactRepository.DeleteAsync).
        builder.HasOne(e => e.Contact).WithMany().HasForeignKey(e => e.ContactId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(e => e.RelatedContact).WithMany().HasForeignKey(e => e.RelatedContactId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(e => e.Organization).WithMany().HasForeignKey(e => e.OrganizationId).IsRequired(false);

        builder.HasIndex(e => new { e.ContactId, e.RelatedContactId }).IsUnique();
        builder.HasIndex(e => e.RelatedContactId);
        builder.HasIndex(e => e.OrganizationId);
        builder.HasIndex(e => e.UserId);
    }
}
