using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.ToTable("Activities");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Type).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Subject).HasMaxLength(512);
        builder.Property(e => e.Body).HasColumnType("nvarchar(max)");
        builder.HasOne(e => e.User).WithMany(u => u.Activities).HasForeignKey(e => e.UserId);
        builder.HasOne(e => e.Contact).WithMany(c => c.Activities).HasForeignKey(e => e.ContactId);
        builder.HasOne(e => e.Deal).WithMany(d => d.Activities).HasForeignKey(e => e.DealId);
    }
}
