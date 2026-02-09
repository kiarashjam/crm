using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class TaskCommentConfiguration : IEntityTypeConfiguration<TaskComment>
{
    public void Configure(EntityTypeBuilder<TaskComment> builder)
    {
        builder.ToTable("TaskComments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Body).HasMaxLength(4096).IsRequired();
        builder.HasOne(e => e.TaskItem).WithMany().HasForeignKey(e => e.TaskItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Author).WithMany().HasForeignKey(e => e.AuthorId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(e => e.TaskItemId);
        builder.HasIndex(e => e.AuthorId);
    }
}
