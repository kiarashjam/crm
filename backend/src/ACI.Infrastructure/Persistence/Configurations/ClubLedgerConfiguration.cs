using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

// The club's typed money ledger (see ClubLedger.cs in ACI.Domain). Money is decimal(18,2)
// with a currency code beside it. The unique indexes below are the database's own guard
// for what the service checks first: one void per entry, one charge per tab and per
// subscription period, one code per gift card, and one result per Idempotency-Key. The
// in-memory provider the HTTP tests use enforces none of them; the Version tokens hold the
// same lines there.

internal sealed class ClubHouseAccountConfiguration : IEntityTypeConfiguration<ClubHouseAccount>
{
    public void Configure(EntityTypeBuilder<ClubHouseAccount> builder)
    {
        builder.ToTable("ClubHouseAccounts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        builder.Property(e => e.Version).IsConcurrencyToken();
        builder.HasIndex(e => e.MemberId).IsUnique();
        builder.HasIndex(e => e.OrganizationId);
    }
}

internal sealed class ClubChargeConfiguration : IEntityTypeConfiguration<ClubCharge>
{
    public void Configure(EntityTypeBuilder<ClubCharge> builder)
    {
        builder.ToTable("ClubCharges");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.MemberName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.EntryType).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Kind).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        builder.Property(e => e.Reference).HasMaxLength(200);
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.Property(e => e.IdempotencyKey).HasMaxLength(100);
        builder.Property(e => e.CreatedByName).HasMaxLength(200);
        builder.Property(e => e.VatRate).HasPrecision(5, 2);
        builder.HasIndex(e => new { e.OrganizationId, e.MemberId, e.PostedAtUtc });
        // What the billing run chases: charges with a due date that may be past it.
        builder.HasIndex(e => new { e.OrganizationId, e.DueAtUtc }).HasFilter("[DueAtUtc] IS NOT NULL");
        builder.HasOne<ClubCharge>().WithMany().HasForeignKey(e => e.ReversesChargeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<ClubTab>().WithMany().HasForeignKey(e => e.TabId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<ClubSubscription>().WithMany().HasForeignKey(e => e.SubscriptionId).OnDelete(DeleteBehavior.NoAction);
        // A charge is voided once, a tab posts once, and a subscription bills a period once.
        builder.HasIndex(e => e.ReversesChargeId).IsUnique().HasFilter("[ReversesChargeId] IS NOT NULL");
        builder.HasIndex(e => e.TabId).IsUnique().HasFilter("[TabId] IS NOT NULL");
        builder.HasIndex(e => new { e.SubscriptionId, e.BillingPeriodStartUtc }).IsUnique()
            .HasFilter("[SubscriptionId] IS NOT NULL AND [BillingPeriodStartUtc] IS NOT NULL");
        builder.HasIndex(e => new { e.OrganizationId, e.IdempotencyKey }).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
    }
}

internal sealed class ClubPaymentConfiguration : IEntityTypeConfiguration<ClubPayment>
{
    public void Configure(EntityTypeBuilder<ClubPayment> builder)
    {
        builder.ToTable("ClubPayments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.MemberName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.EntryType).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Method).HasMaxLength(20);
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.Reference).HasMaxLength(200);
        builder.Property(e => e.Notes).HasMaxLength(1000);
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.Property(e => e.IdempotencyKey).HasMaxLength(100);
        builder.Property(e => e.Provider).HasMaxLength(20);
        builder.Property(e => e.ProviderPaymentId).HasMaxLength(200);
        builder.Property(e => e.CreatedByName).HasMaxLength(200);
        builder.HasIndex(e => new { e.OrganizationId, e.MemberId, e.ReceivedAtUtc });
        builder.HasOne<ClubPayment>().WithMany().HasForeignKey(e => e.ReversesPaymentId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(e => e.ReversesPaymentId);
        // A payment is voided once; it may be refunded in parts.
        builder.HasIndex(e => e.ReversesPaymentId, "IX_ClubPayments_OneVoid").IsUnique()
            .HasFilter("[ReversesPaymentId] IS NOT NULL AND [EntryType] = N'Void'");
        builder.HasIndex(e => new { e.OrganizationId, e.IdempotencyKey }).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
    }
}

internal sealed class ClubPaymentAllocationConfiguration : IEntityTypeConfiguration<ClubPaymentAllocation>
{
    public void Configure(EntityTypeBuilder<ClubPaymentAllocation> builder)
    {
        builder.ToTable("ClubPaymentAllocations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.HasOne<ClubPayment>().WithMany().HasForeignKey(e => e.PaymentId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<ClubCharge>().WithMany().HasForeignKey(e => e.ChargeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<ClubPayment>().WithMany().HasForeignKey(e => e.ReversalId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(e => e.OrganizationId);
    }
}

internal sealed class ClubSubscriptionConfiguration : IEntityTypeConfiguration<ClubSubscription>
{
    public void Configure(EntityTypeBuilder<ClubSubscription> builder)
    {
        builder.ToTable("ClubSubscriptions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.MemberName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        builder.Property(e => e.Cadence).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(1000);
        builder.Property(e => e.CreatedByName).HasMaxLength(200);
        builder.Property(e => e.Version).IsConcurrencyToken();
        builder.HasIndex(e => new { e.OrganizationId, e.MemberId });
    }
}

internal sealed class ClubGiftCardConfiguration : IEntityTypeConfiguration<ClubGiftCard>
{
    public void Configure(EntityTypeBuilder<ClubGiftCard> builder)
    {
        builder.ToTable("ClubGiftCards");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Code).HasMaxLength(32).IsRequired();
        builder.Property(e => e.FaceValue).HasPrecision(18, 2);
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        builder.Property(e => e.PurchaserName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.RecipientName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.RecipientEmail).HasMaxLength(256);
        builder.Property(e => e.Message).HasMaxLength(1000);
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.CreatedByName).HasMaxLength(200);
        builder.Property(e => e.Version).IsConcurrencyToken();
        builder.HasIndex(e => new { e.OrganizationId, e.Code }).IsUnique();
    }
}

internal sealed class ClubGiftCardTransactionConfiguration : IEntityTypeConfiguration<ClubGiftCardTransaction>
{
    public void Configure(EntityTypeBuilder<ClubGiftCardTransaction> builder)
    {
        builder.ToTable("ClubGiftCardTransactions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Type).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.Property(e => e.AppliedTo).HasMaxLength(500);
        builder.Property(e => e.RedeemedByName).HasMaxLength(200);
        builder.Property(e => e.IdempotencyKey).HasMaxLength(100);
        builder.Property(e => e.CreatedByName).HasMaxLength(200);
        builder.HasOne<ClubGiftCard>().WithMany().HasForeignKey(e => e.GiftCardId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(e => new { e.GiftCardId, e.AtUtc });
        builder.HasIndex(e => e.OrganizationId);
        // A card is voided once and expires once.
        builder.HasIndex(e => e.GiftCardId, "IX_ClubGiftCardTransactions_OneVoid").IsUnique().HasFilter("[Type] = N'Void'");
        builder.HasIndex(e => e.GiftCardId, "IX_ClubGiftCardTransactions_OneExpiry").IsUnique().HasFilter("[Type] = N'Expiry'");
        builder.HasIndex(e => new { e.OrganizationId, e.IdempotencyKey }).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
    }
}

internal sealed class ClubTabConfiguration : IEntityTypeConfiguration<ClubTab>
{
    public void Configure(EntityTypeBuilder<ClubTab> builder)
    {
        builder.ToTable("ClubTabs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.MemberName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.MemberTier).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Venue).HasMaxLength(32).IsRequired();
        builder.Property(e => e.ServerName).HasMaxLength(200);
        builder.Property(e => e.Notes).HasMaxLength(1000);
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Total).HasPrecision(18, 2);
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        builder.Property(e => e.ClosedByName).HasMaxLength(200);
        builder.Property(e => e.VoidedByName).HasMaxLength(200);
        builder.Property(e => e.VoidReason).HasMaxLength(500);
        builder.Property(e => e.CreatedByName).HasMaxLength(200);
        builder.Property(e => e.Version).IsConcurrencyToken();
        builder.HasIndex(e => new { e.OrganizationId, e.Status });
    }
}

internal sealed class ClubTabItemConfiguration : IEntityTypeConfiguration<ClubTabItem>
{
    public void Configure(EntityTypeBuilder<ClubTabItem> builder)
    {
        builder.ToTable("ClubTabItems");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Quantity).HasPrecision(9, 2);
        builder.Property(e => e.UnitPrice).HasPrecision(18, 2);
        builder.Property(e => e.VatRate).HasPrecision(5, 2);
        builder.Property(e => e.AddedByName).HasMaxLength(200);
        builder.Property(e => e.RemovedByName).HasMaxLength(200);
        builder.HasOne<ClubTab>().WithMany().HasForeignKey(e => e.TabId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(e => e.OrganizationId);
    }
}

internal sealed class ClubDonationConfiguration : IEntityTypeConfiguration<ClubDonation>
{
    public void Configure(EntityTypeBuilder<ClubDonation> builder)
    {
        builder.ToTable("ClubDonations");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.MemberName).HasMaxLength(200);
        builder.Property(e => e.EntryType).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Cause).HasMaxLength(60).IsRequired();
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        builder.Property(e => e.Message).HasMaxLength(1000);
        builder.Property(e => e.AcknowledgedByName).HasMaxLength(200);
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.Property(e => e.CreatedByName).HasMaxLength(200);
        builder.Property(e => e.Version).IsConcurrencyToken();
        builder.HasOne<ClubDonation>().WithMany().HasForeignKey(e => e.ReversesDonationId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(e => e.ReversesDonationId).IsUnique().HasFilter("[ReversesDonationId] IS NOT NULL");
        builder.HasIndex(e => new { e.OrganizationId, e.ReceivedAtUtc });
    }
}
