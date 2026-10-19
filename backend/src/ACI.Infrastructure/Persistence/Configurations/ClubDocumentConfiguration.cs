using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class ClubDocumentConfiguration : IEntityTypeConfiguration<ClubDocument>
{
    public void Configure(EntityTypeBuilder<ClubDocument> builder)
    {
        builder.ToTable("ClubDocuments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Kind).HasMaxLength(40).IsRequired();
        builder.Property(e => e.Payload).IsRequired();
        builder.HasIndex(e => new { e.OrganizationId, e.Kind });
    }
}
