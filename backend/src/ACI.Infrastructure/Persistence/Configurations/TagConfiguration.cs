using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("Tags");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(Tag.MaxNameLength).IsRequired();
        builder.Property(e => e.Color).HasMaxLength(16).IsRequired();
        builder.HasIndex(e => new { e.OrganizationId, e.Name });
    }
}

internal sealed class RecordTagConfiguration : IEntityTypeConfiguration<RecordTag>
{
    public void Configure(EntityTypeBuilder<RecordTag> builder)
    {
        builder.ToTable("RecordTags");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EntityType).HasMaxLength(16).IsRequired();
        builder.HasOne(e => e.Tag).WithMany().HasForeignKey(e => e.TagId).OnDelete(DeleteBehavior.Cascade);
        // A tag once per record, and "the records with this tag" straight from the index.
        builder.HasIndex(e => new { e.TagId, e.RecordId }).IsUnique();
        // A record's tags, for its page, a page of a list, and removal with the record.
        builder.HasIndex(e => new { e.RecordId, e.TagId });
        builder.HasIndex(e => new { e.OrganizationId, e.EntityType });
    }
}

internal sealed class SavedViewConfiguration : IEntityTypeConfiguration<SavedView>
{
    public void Configure(EntityTypeBuilder<SavedView> builder)
    {
        builder.ToTable("SavedViews");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.List).HasMaxLength(24).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(SavedView.MaxNameLength).IsRequired();
        builder.Property(e => e.QueryJson).IsRequired();
        builder.Property(e => e.Version).IsConcurrencyToken();
        builder.HasIndex(e => new { e.OrganizationId, e.List });
    }
}

internal sealed class SavedViewDefaultConfiguration : IEntityTypeConfiguration<SavedViewDefault>
{
    public void Configure(EntityTypeBuilder<SavedViewDefault> builder)
    {
        builder.ToTable("SavedViewDefaults");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.List).HasMaxLength(24).IsRequired();
        builder.HasOne(e => e.SavedView).WithMany().HasForeignKey(e => e.SavedViewId).OnDelete(DeleteBehavior.Cascade);
        // One default per person, workspace and list. Unfiltered, so SQL Server counts a
        // personal workspace's NULL organization as one value and it holds there too.
        builder.HasIndex(e => new { e.UserId, e.OrganizationId, e.List }).IsUnique().HasFilter(null);
        builder.HasIndex(e => e.SavedViewId);
    }
}
