using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

// Private events, their menu and services, and their guest lists (see ClubPrivateEvents.cs in
// ACI.Domain). Money is decimal(18,2) like the ledger's; the event's charges live on the ledger.

internal sealed class ClubPrivateEventConfiguration : IEntityTypeConfiguration<ClubPrivateEvent>
{
    public void Configure(EntityTypeBuilder<ClubPrivateEvent> builder)
    {
        builder.ToTable("ClubPrivateEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Title).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(2000);
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        builder.Property(e => e.DepositAmount).HasPrecision(18, 2);
        builder.Property(e => e.CancelReason).HasMaxLength(500);
        builder.Property(e => e.CancelledByName).HasMaxLength(200);
        builder.Property(e => e.CreatedByName).HasMaxLength(200);
        builder.Property(e => e.Version).IsConcurrencyToken();
        builder.HasIndex(e => new { e.OrganizationId, e.StartAtUtc });
        builder.HasIndex(e => new { e.OrganizationId, e.CompanyId }).HasFilter("[CompanyId] IS NOT NULL");
        builder.HasIndex(e => new { e.OrganizationId, e.ContactId }).HasFilter("[ContactId] IS NOT NULL");
        builder.HasIndex(e => e.ContractId).HasFilter("[ContractId] IS NOT NULL");
    }
}

internal sealed class ClubPrivateEventLineConfiguration : IEntityTypeConfiguration<ClubPrivateEventLine>
{
    public void Configure(EntityTypeBuilder<ClubPrivateEventLine> builder)
    {
        builder.ToTable("ClubPrivateEventLines");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Quantity).HasPrecision(9, 2);
        builder.Property(e => e.UnitPrice).HasPrecision(18, 2);
        builder.Property(e => e.VatRate).HasPrecision(5, 2);
        builder.HasOne<ClubPrivateEvent>().WithMany().HasForeignKey(e => e.PrivateEventId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.OrganizationId, e.PrivateEventId });
    }
}

internal sealed class ClubPrivateEventGuestConfiguration : IEntityTypeConfiguration<ClubPrivateEventGuest>
{
    public void Configure(EntityTypeBuilder<ClubPrivateEventGuest> builder)
    {
        builder.ToTable("ClubPrivateEventGuests");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(320);
        builder.Property(e => e.Notes).HasMaxLength(500);
        builder.Property(e => e.CheckedInByName).HasMaxLength(200);
        builder.Property(e => e.AddedByName).HasMaxLength(200);
        builder.HasOne<ClubPrivateEvent>().WithMany().HasForeignKey(e => e.PrivateEventId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.OrganizationId, e.PrivateEventId });
        builder.HasIndex(e => new { e.OrganizationId, e.MemberId }).HasFilter("[MemberId] IS NOT NULL");
    }
}
