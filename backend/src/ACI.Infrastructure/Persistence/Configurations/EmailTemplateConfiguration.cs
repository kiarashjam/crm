using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class EmailTemplateConfiguration : IEntityTypeConfiguration<EmailTemplate>
{
    public void Configure(EntityTypeBuilder<EmailTemplate> builder)
    {
        builder.ToTable("EmailTemplates");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Category).HasMaxLength(60);
        builder.Property(e => e.Description).HasMaxLength(300);
        builder.Property(e => e.StarterKey).HasMaxLength(60);
        builder.HasIndex(e => new { e.OrganizationId, e.Name }).HasDatabaseName("IX_EmailTemplates_OrganizationId_Name");
        // The starter set is given once per workspace: a second first read at the same time
        // cannot add it twice.
        builder.HasIndex(e => new { e.OrganizationId, e.StarterKey })
            .IsUnique()
            .HasFilter("[StarterKey] IS NOT NULL")
            .HasDatabaseName("UX_EmailTemplates_OrganizationId_StarterKey");
        builder.HasMany(e => e.Versions)
            .WithOne()
            .HasForeignKey(v => v.EmailTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class EmailTemplateVersionConfiguration : IEntityTypeConfiguration<EmailTemplateVersion>
{
    public void Configure(EntityTypeBuilder<EmailTemplateVersion> builder)
    {
        builder.ToTable("EmailTemplateVersions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Language).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Subject).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Body).IsRequired();
        // One version per language.
        builder.HasIndex(e => new { e.EmailTemplateId, e.Language }).IsUnique().HasDatabaseName("UX_EmailTemplateVersions_Template_Language");
    }
}
