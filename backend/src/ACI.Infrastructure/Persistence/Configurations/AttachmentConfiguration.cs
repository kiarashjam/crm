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
        builder.Property(e => e.Currency).HasMaxLength(3);
        builder.Property(e => e.LinkEntityType).HasMaxLength(16);
        builder.HasIndex(e => new { e.OrganizationId, e.EntityType });
    }
}

internal sealed class CustomFieldEntryConfiguration : IEntityTypeConfiguration<CustomFieldEntry>
{
    public void Configure(EntityTypeBuilder<CustomFieldEntry> builder)
    {
        builder.ToTable("CustomFieldEntries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EntityType).HasMaxLength(16).IsRequired();
        builder.Property(e => e.TextValue).HasMaxLength(2000);
        builder.Property(e => e.TextKey).HasMaxLength(CustomFieldEntry.TextKeyLength);
        // Numbers as people type them (a share, a measurement) and money to the cent alike.
        builder.Property(e => e.NumberValue).HasPrecision(28, 8);
        builder.Property(e => e.Currency).HasMaxLength(3);
        builder.HasOne<CustomFieldDefinition>().WithMany().HasForeignKey(e => e.FieldId).OnDelete(DeleteBehavior.Cascade);

        // A record's values, and one row per field (per option for a multi select): a
        // second save racing the first cannot leave the field with two values. Unfiltered,
        // so SQL Server allows one NULL TextKey per record and field, which is every field
        // but text, select and multi select (EF would otherwise leave NULLs out of it).
        builder.HasIndex(e => new { e.RecordId, e.FieldId, e.TextKey }).IsUnique().HasFilter(null);
        // Filtering and sorting by a field: the workspace (which the query filter adds),
        // the field, the value, and the record it belongs to, answered from the index.
        builder.HasIndex(e => new { e.OrganizationId, e.FieldId, e.TextKey }).IncludeProperties(e => e.RecordId);
        builder.HasIndex(e => new { e.OrganizationId, e.FieldId, e.NumberValue }).IncludeProperties(e => new { e.RecordId, e.BoolValue });
        builder.HasIndex(e => new { e.OrganizationId, e.FieldId, e.DateValue }).IncludeProperties(e => e.RecordId);
        builder.HasIndex(e => new { e.OrganizationId, e.FieldId, e.LinkId }).IncludeProperties(e => e.RecordId);
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
