using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class SalesTeamConfiguration : IEntityTypeConfiguration<SalesTeam>
{
    public void Configure(EntityTypeBuilder<SalesTeam> builder)
    {
        builder.ToTable("SalesTeams");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(120).IsRequired();
        builder.HasIndex(e => e.OrganizationId);
    }
}

internal sealed class SalesTeamMemberConfiguration : IEntityTypeConfiguration<SalesTeamMember>
{
    public void Configure(EntityTypeBuilder<SalesTeamMember> builder)
    {
        builder.ToTable("SalesTeamMembers");
        builder.HasKey(e => new { e.TeamId, e.UserId });
    }
}
