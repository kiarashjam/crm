using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("TaskItems");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Title).HasMaxLength(512).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(2048);
        builder.HasOne(e => e.User).WithMany(u => u.TaskItems).HasForeignKey(e => e.UserId);
        builder.HasOne(e => e.Lead).WithMany(l => l.TaskItems).HasForeignKey(e => e.LeadId);
        builder.HasOne(e => e.Deal).WithMany(d => d.TaskItems).HasForeignKey(e => e.DealId);
    }
}
