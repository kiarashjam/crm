using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

// Payments from outside the club and its accounting (see ClubPayments.cs in ACI.Domain).
// The unique indexes are the database's own guard for what has to happen once: one
// settings row per workspace, one saved method per member and provider, one attempt per
// claim key and per Stripe payment, one answer per Stripe event, one reference per number
// and per statement or charge, and one record per bank transfer. Every one of them starts
// with the organization, so a copy of a workspace (OrganizationCloner) can hold the same
// provider ids as the original.

internal sealed class ClubPaymentSettingsConfiguration : IEntityTypeConfiguration<ClubPaymentSettings>
{
    public void Configure(EntityTypeBuilder<ClubPaymentSettings> builder)
    {
        builder.ToTable("ClubPaymentSettings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.StripeSecretKeyProtected).HasMaxLength(2000);
        builder.Property(e => e.StripeKeyHint).HasMaxLength(40);
        builder.Property(e => e.StripeWebhookSecretProtected).HasMaxLength(2000);
        builder.Property(e => e.Iban).HasMaxLength(34);
        builder.Property(e => e.QrIban).HasMaxLength(34);
        builder.Property(e => e.QrReferencePrefix).HasMaxLength(12);
        builder.Property(e => e.CreditorName).HasMaxLength(70);
        builder.Property(e => e.CreditorStreet).HasMaxLength(70);
        builder.Property(e => e.CreditorBuildingNumber).HasMaxLength(16);
        builder.Property(e => e.CreditorPostalCode).HasMaxLength(16);
        builder.Property(e => e.CreditorTown).HasMaxLength(35);
        builder.Property(e => e.CreditorCountry).HasMaxLength(2);
        builder.Property(e => e.AccountsJson).HasMaxLength(4000);
        builder.Property(e => e.UpdatedByName).HasMaxLength(200);
        builder.Property(e => e.Version).IsConcurrencyToken();
        builder.HasIndex(e => e.OrganizationId).IsUnique().HasFilter("[OrganizationId] IS NOT NULL");
        // Stripe's posts are found by the key in their address, across workspaces.
        builder.HasIndex(e => e.WebhookKey).IsUnique();
    }
}

internal sealed class ClubPaymentProfileConfiguration : IEntityTypeConfiguration<ClubPaymentProfile>
{
    public void Configure(EntityTypeBuilder<ClubPaymentProfile> builder)
    {
        builder.ToTable("ClubPaymentProfiles");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Provider).HasMaxLength(20).IsRequired();
        builder.Property(e => e.CustomerId).HasMaxLength(100);
        builder.Property(e => e.PaymentMethodId).HasMaxLength(100);
        builder.Property(e => e.PaymentMethodType).HasMaxLength(20);
        builder.Property(e => e.Brand).HasMaxLength(20);
        builder.Property(e => e.Last4).HasMaxLength(4);
        builder.Property(e => e.AutoPayAgreedBy).HasMaxLength(200);
        builder.Property(e => e.Version).IsConcurrencyToken();
        builder.HasIndex(e => new { e.OrganizationId, e.MemberId, e.Provider }).IsUnique();
    }
}

internal sealed class ClubOnlinePaymentConfiguration : IEntityTypeConfiguration<ClubOnlinePayment>
{
    public void Configure(EntityTypeBuilder<ClubOnlinePayment> builder)
    {
        builder.ToTable("ClubOnlinePayments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.MemberName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Provider).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Purpose).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.AmountReceived).HasPrecision(18, 2);
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500).IsRequired();
        builder.Property(e => e.ChargeIds).HasMaxLength(4000);
        builder.Property(e => e.AttemptKey).HasMaxLength(100);
        builder.Property(e => e.CheckoutSessionId).HasMaxLength(200);
        builder.Property(e => e.CheckoutUrl).HasMaxLength(2000);
        builder.Property(e => e.PaymentIntentId).HasMaxLength(100);
        builder.Property(e => e.SetupIntentId).HasMaxLength(100);
        builder.Property(e => e.ProviderChargeId).HasMaxLength(100);
        builder.Property(e => e.MethodType).HasMaxLength(30);
        builder.Property(e => e.FailureCode).HasMaxLength(100);
        builder.Property(e => e.FailureMessage).HasMaxLength(500);
        builder.Property(e => e.CreatedByName).HasMaxLength(200);
        builder.Property(e => e.Version).IsConcurrencyToken();
        builder.HasIndex(e => new { e.OrganizationId, e.MemberId, e.CreatedAtUtc });
        builder.HasIndex(e => new { e.OrganizationId, e.Status });
        builder.HasIndex(e => new { e.OrganizationId, e.AttemptKey }).IsUnique().HasFilter("[AttemptKey] IS NOT NULL");
        builder.HasIndex(e => new { e.OrganizationId, e.PaymentIntentId }).IsUnique().HasFilter("[PaymentIntentId] IS NOT NULL");
        builder.HasIndex(e => new { e.OrganizationId, e.CheckoutSessionId }).IsUnique().HasFilter("[CheckoutSessionId] IS NOT NULL");
    }
}

internal sealed class ClubProviderEventConfiguration : IEntityTypeConfiguration<ClubProviderEvent>
{
    public void Configure(EntityTypeBuilder<ClubProviderEvent> builder)
    {
        builder.ToTable("ClubProviderEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Provider).HasMaxLength(20).IsRequired();
        builder.Property(e => e.EventId).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Type).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ObjectId).HasMaxLength(100);
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Note).HasMaxLength(500);
        builder.HasIndex(e => new { e.OrganizationId, e.Provider, e.EventId }).IsUnique();
    }
}

internal sealed class ClubPaymentReferenceConfiguration : IEntityTypeConfiguration<ClubPaymentReference>
{
    public void Configure(EntityTypeBuilder<ClubPaymentReference> builder)
    {
        builder.ToTable("ClubPaymentReferences");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.QrReference).HasMaxLength(27);
        builder.Property(e => e.ScorReference).HasMaxLength(25).IsRequired();
        builder.Property(e => e.Purpose).HasMaxLength(20).IsRequired();
        builder.Property(e => e.AmountDue).HasPrecision(18, 2);
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        builder.HasIndex(e => new { e.OrganizationId, e.Number }).IsUnique();
        builder.HasIndex(e => new { e.OrganizationId, e.QrReference }).IsUnique().HasFilter("[QrReference] IS NOT NULL");
        builder.HasIndex(e => new { e.OrganizationId, e.ScorReference }).IsUnique();
        // A statement, or a charge, keeps the reference it was first printed with.
        builder.HasIndex(e => new { e.OrganizationId, e.MemberId, e.PeriodStartUtc, e.PeriodEndUtc }).IsUnique()
            .HasFilter("[Purpose] = N'statement'");
        builder.HasIndex(e => new { e.OrganizationId, e.ChargeId }).IsUnique().HasFilter("[ChargeId] IS NOT NULL");
        // A contract's deposit keeps the reference it was first printed with, too.
        builder.HasIndex(e => new { e.OrganizationId, e.ContractId }).IsUnique().HasFilter("[ContractId] IS NOT NULL");
    }
}

internal sealed class ContractDepositConfiguration : IEntityTypeConfiguration<ContractDeposit>
{
    public void Configure(EntityTypeBuilder<ContractDeposit> builder)
    {
        builder.ToTable("ContractDeposits");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Provider).HasMaxLength(20);
        builder.Property(e => e.ProviderPaymentId).HasMaxLength(200);
        builder.Property(e => e.MethodType).HasMaxLength(30);
        builder.Property(e => e.AmountPaid).HasPrecision(18, 2);
        builder.Property(e => e.AmountRefunded).HasPrecision(18, 2);
        builder.Property(e => e.CheckoutSessionId).HasMaxLength(200);
        builder.Property(e => e.CheckoutUrl).HasMaxLength(2000);
        builder.Property(e => e.FailureMessage).HasMaxLength(500);
        builder.Property(e => e.Version).IsConcurrencyToken();
        // One deposit per contract; the database refuses a second, so two sends at once make one.
        builder.HasIndex(e => new { e.OrganizationId, e.ContractId }).IsUnique();
        // Stripe's word on a payment, and a refund made in its dashboard, find the deposit by it.
        builder.HasIndex(e => new { e.OrganizationId, e.ProviderPaymentId }).HasFilter("[ProviderPaymentId] IS NOT NULL");
        builder.HasIndex(e => new { e.OrganizationId, e.MemberId }).HasFilter("[MemberId] IS NOT NULL");
    }
}

internal sealed class ClubBankImportConfiguration : IEntityTypeConfiguration<ClubBankImport>
{
    public void Configure(EntityTypeBuilder<ClubBankImport> builder)
    {
        builder.ToTable("ClubBankImports");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.FileName).HasMaxLength(260).IsRequired();
        builder.Property(e => e.Format).HasMaxLength(20).IsRequired();
        builder.Property(e => e.MessageId).HasMaxLength(100);
        builder.Property(e => e.AccountIban).HasMaxLength(34);
        builder.Property(e => e.AmountMatched).HasPrecision(18, 2);
        builder.Property(e => e.ImportedByName).HasMaxLength(200);
        builder.HasIndex(e => new { e.OrganizationId, e.ImportedAtUtc });
    }
}

internal sealed class ClubBankTransactionConfiguration : IEntityTypeConfiguration<ClubBankTransaction>
{
    public void Configure(EntityTypeBuilder<ClubBankTransaction> builder)
    {
        builder.ToTable("ClubBankTransactions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.BankReference).HasMaxLength(100).IsRequired();
        builder.Property(e => e.AccountIban).HasMaxLength(34);
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.Currency).HasMaxLength(3).IsRequired();
        builder.Property(e => e.ReferenceType).HasMaxLength(10);
        builder.Property(e => e.Reference).HasMaxLength(40);
        builder.Property(e => e.DebtorName).HasMaxLength(140);
        builder.Property(e => e.DebtorIban).HasMaxLength(34);
        builder.Property(e => e.Message).HasMaxLength(500);
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Note).HasMaxLength(500);
        builder.Property(e => e.MemberName).HasMaxLength(200);
        builder.Property(e => e.ResolvedByName).HasMaxLength(200);
        builder.Property(e => e.Version).IsConcurrencyToken();
        builder.HasOne<ClubBankImport>().WithMany().HasForeignKey(e => e.ImportId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(e => new { e.OrganizationId, e.BankReference }).IsUnique();
        builder.HasIndex(e => new { e.OrganizationId, e.Status });
    }
}
