using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

// Sales, the POS's tickets, its connection and members' codes (see ClubPos.cs in ACI.Domain).
// The unique indexes are the database's own guard for what has to happen once: one sale per
// Idempotency-Key (a POS ticket's is its own id), one ticket per id the POS gave it, one
// connection per workspace, and one member per code. Every one starts with the organization,
// so a copy of a workspace (OrganizationCloner) can hold the same POS ids as the original.

internal sealed class ClubSaleConfiguration : IEntityTypeConfiguration<ClubSale>
{
    public void Configure(EntityTypeBuilder<ClubSale> builder)
    {
        builder.ToTable("ClubSales");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.MemberName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Source).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Venue).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Tender).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Subtotal).HasPrecision(18, 2);
        builder.Property(e => e.Discount).HasPrecision(18, 2);
        builder.Property(e => e.DiscountReason).HasMaxLength(500);
        builder.Property(e => e.Total).HasPrecision(18, 2);
        builder.Property(e => e.PaidAmount).HasPrecision(18, 2);
        builder.Property(e => e.OwedAmount).HasPrecision(18, 2);
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        builder.Property(e => e.ChargeIds).HasMaxLength(2000);
        builder.Property(e => e.PaymentIds).HasMaxLength(1000);
        builder.Property(e => e.Reference).HasMaxLength(200);
        builder.Property(e => e.StaffName).HasMaxLength(200);
        builder.Property(e => e.TableName).HasMaxLength(100);
        builder.Property(e => e.Notes).HasMaxLength(1000);
        builder.Property(e => e.IdempotencyKey).HasMaxLength(300);
        builder.Property(e => e.CreatedByName).HasMaxLength(200);
        builder.HasIndex(e => new { e.OrganizationId, e.MemberId, e.CreatedAtUtc });
        builder.HasIndex(e => new { e.OrganizationId, e.IdempotencyKey }).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
        // A ticket from the POS is posted as one sale.
        builder.HasIndex(e => new { e.OrganizationId, e.PosTicketId }).IsUnique().HasFilter("[PosTicketId] IS NOT NULL");
    }
}

internal sealed class ClubSaleLineConfiguration : IEntityTypeConfiguration<ClubSaleLine>
{
    public void Configure(EntityTypeBuilder<ClubSaleLine> builder)
    {
        builder.ToTable("ClubSaleLines");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Sku).HasMaxLength(100);
        builder.Property(e => e.Quantity).HasPrecision(18, 2);
        builder.Property(e => e.UnitPrice).HasPrecision(18, 2);
        builder.Property(e => e.Discount).HasPrecision(18, 2);
        builder.Property(e => e.Total).HasPrecision(18, 2);
        builder.Property(e => e.VatRate).HasPrecision(5, 2);
        builder.Property(e => e.Kind).HasMaxLength(20).IsRequired();
        builder.HasOne<ClubSale>().WithMany().HasForeignKey(e => e.SaleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.OrganizationId, e.SaleId });
    }
}

internal sealed class ClubMemberCodeConfiguration : IEntityTypeConfiguration<ClubMemberCode>
{
    public void Configure(EntityTypeBuilder<ClubMemberCode> builder)
    {
        builder.ToTable("ClubMemberCodes");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Kind).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Code).HasMaxLength(120).IsRequired();
        builder.Property(e => e.Label).HasMaxLength(100);
        builder.Property(e => e.CreatedByName).HasMaxLength(200);
        // A code names one member: a ticket that carries it is theirs.
        builder.HasIndex(e => new { e.OrganizationId, e.Kind, e.Code }).IsUnique();
        builder.HasIndex(e => new { e.OrganizationId, e.MemberId });
    }
}

internal sealed class ClubPosSettingsConfiguration : IEntityTypeConfiguration<ClubPosSettings>
{
    public void Configure(EntityTypeBuilder<ClubPosSettings> builder)
    {
        builder.ToTable("ClubPosSettings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Provider).HasMaxLength(20).IsRequired();
        builder.Property(e => e.RefreshTokenProtected).HasMaxLength(4000);
        builder.Property(e => e.AccessTokenProtected).HasMaxLength(4000);
        builder.Property(e => e.ConnectedByName).HasMaxLength(200);
        builder.Property(e => e.LocationId).HasMaxLength(100);
        builder.Property(e => e.LocationName).HasMaxLength(200);
        builder.Property(e => e.MethodMapJson).HasMaxLength(4000);
        builder.Property(e => e.DefaultVenue).HasMaxLength(20).IsRequired();
        builder.Property(e => e.LastImportError).HasMaxLength(500);
        builder.Property(e => e.UpdatedByName).HasMaxLength(200);
        builder.Property(e => e.Version).IsConcurrencyToken();
        builder.HasIndex(e => e.OrganizationId).IsUnique().HasFilter("[OrganizationId] IS NOT NULL");
    }
}

internal sealed class ClubPosTicketConfiguration : IEntityTypeConfiguration<ClubPosTicket>
{
    public void Configure(EntityTypeBuilder<ClubPosTicket> builder)
    {
        builder.ToTable("ClubPosTickets");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Provider).HasMaxLength(20).IsRequired();
        builder.Property(e => e.ExternalId).HasMaxLength(200).IsRequired();
        builder.Property(e => e.ReceiptNumber).HasMaxLength(100);
        builder.Property(e => e.LocationId).HasMaxLength(100);
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        builder.Property(e => e.Total).HasPrecision(18, 2);
        builder.Property(e => e.OnAccountAmount).HasPrecision(18, 2);
        builder.Property(e => e.PaidAmount).HasPrecision(18, 2);
        builder.Property(e => e.TipAmount).HasPrecision(18, 2);
        builder.Property(e => e.TendersText).HasMaxLength(500);
        builder.Property(e => e.StaffName).HasMaxLength(200);
        builder.Property(e => e.TableName).HasMaxLength(100);
        builder.Property(e => e.CustomerName).HasMaxLength(200);
        builder.Property(e => e.CustomerReference).HasMaxLength(500);
        builder.Property(e => e.CustomerId).HasMaxLength(100);
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Note).HasMaxLength(500);
        builder.Property(e => e.MemberName).HasMaxLength(200);
        builder.Property(e => e.MatchedBy).HasMaxLength(20);
        builder.Property(e => e.ResolvedByName).HasMaxLength(200);
        builder.Property(e => e.Version).IsConcurrencyToken();
        // Each import reads back a while, and gets the tickets it already has: once each.
        builder.HasIndex(e => new { e.OrganizationId, e.Provider, e.ExternalId }).IsUnique();
        builder.HasIndex(e => new { e.OrganizationId, e.Status, e.ClosedAtUtc });
        builder.HasIndex(e => new { e.OrganizationId, e.MemberId }).HasFilter("[MemberId] IS NOT NULL");
    }
}
