using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("Attachments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EntityType).HasMaxLength(16).IsRequired();
        builder.Property(e => e.FileName).HasMaxLength(255).IsRequired();
        builder.Property(e => e.ContentType).HasMaxLength(127).IsRequired();
        builder.Property(e => e.UploadedByName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Content).IsRequired();
        builder.HasIndex(e => new { e.EntityType, e.RecordId });
    }
}

internal sealed class CustomFieldDefinitionConfiguration : IEntityTypeConfiguration<CustomFieldDefinition>
{
    public void Configure(EntityTypeBuilder<CustomFieldDefinition> builder)
    {
        builder.ToTable("CustomFieldDefinitions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EntityType).HasMaxLength(16).IsRequired();
        builder.Property(e => e.Key).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Label).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Type).HasMaxLength(16).IsRequired();
        builder.HasIndex(e => new { e.OrganizationId, e.EntityType });
    }
}

internal sealed class CustomFieldValueSetConfiguration : IEntityTypeConfiguration<CustomFieldValueSet>
{
    public void Configure(EntityTypeBuilder<CustomFieldValueSet> builder)
    {
        builder.ToTable("CustomFieldValues");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EntityType).HasMaxLength(16).IsRequired();
        builder.Property(e => e.ValuesJson).IsRequired();
        // Record ids are unique across the whole database, so one set per record.
        builder.HasIndex(e => new { e.EntityType, e.RecordId }).IsUnique();
    }
}
