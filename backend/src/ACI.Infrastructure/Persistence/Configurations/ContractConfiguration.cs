using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

public class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.ToTable("Contracts");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Status).IsRequired().HasMaxLength(32);
        builder.Property(c => c.Title).IsRequired().HasMaxLength(300);
        // No length cap: a contract is as long as it is, and a truncated one is
        // worse than a large column.
        builder.Property(c => c.Body).IsRequired();

        builder.Property(c => c.CounterpartyName).HasMaxLength(300);
        builder.Property(c => c.CounterpartyEmail).HasMaxLength(320);

        builder.Property(c => c.BodyHashAtSend).HasMaxLength(64);
        builder.Property(c => c.SigningTokenHash).HasMaxLength(64);

        builder.Property(c => c.ClientSignatureName).HasMaxLength(300);
        builder.Property(c => c.ClientSignatureIp).HasMaxLength(64);
        builder.Property(c => c.ClientSignatureUserAgent).HasMaxLength(512);
        builder.Property(c => c.CounterSignatureName).HasMaxLength(300);
        builder.Property(c => c.CounterSignatureIp).HasMaxLength(64);
        builder.Property(c => c.ClosedReason).HasMaxLength(1000);

        // Every authenticated read is scoped by organisation, and the lead panel
        // lists by lead.
        builder.HasIndex(c => new { c.OrganizationId, c.LeadId });

        // The public signing path looks up by this and nothing else, so it has to
        // be indexed. Unique because two contracts sharing a token would make the
        // link ambiguous; filtered so the many nulls (drafts, voided) do not
        // collide with each other.
        builder.HasIndex(c => c.SigningTokenHash)
            .IsUnique()
            .HasFilter("[SigningTokenHash] IS NOT NULL");

        builder.HasMany(c => c.Events)
            .WithOne(e => e.Contract!)
            .HasForeignKey(e => e.ContractId)
            // A contract's audit trail has no meaning without the contract.
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ContractEventConfiguration : IEntityTypeConfiguration<ContractEvent>
{
    public void Configure(EntityTypeBuilder<ContractEvent> builder)
    {
        builder.ToTable("ContractEvents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Type).IsRequired().HasMaxLength(32);
        builder.Property(e => e.Detail).HasMaxLength(1000);
        builder.Property(e => e.ActorLabel).HasMaxLength(300);
        builder.Property(e => e.Ip).HasMaxLength(64);
        builder.Property(e => e.UserAgent).HasMaxLength(512);

        builder.HasIndex(e => new { e.ContractId, e.AtUtc });
    }
}
