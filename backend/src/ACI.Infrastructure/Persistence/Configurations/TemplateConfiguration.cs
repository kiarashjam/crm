using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class TemplateConfiguration : IEntityTypeConfiguration<Template>
{
    public void Configure(EntityTypeBuilder<Template> builder)
    {
        builder.ToTable("Templates");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Title).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(1000);
        builder.Property(e => e.Category).HasMaxLength(128).IsRequired();
        builder.Property(e => e.Goal).HasMaxLength(512).IsRequired();
        builder.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).IsRequired(false);
    }
}
