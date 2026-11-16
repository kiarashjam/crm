using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ACI.Infrastructure.Persistence.Configurations;

internal sealed class UserExternalLoginConfiguration : IEntityTypeConfiguration<UserExternalLogin>
{
    public void Configure(EntityTypeBuilder<UserExternalLogin> builder)
    {
        builder.ToTable("UserExternalLogins");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Provider).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Issuer).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Subject).HasMaxLength(255).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(256).IsRequired();
        // One provider account signs in as one person.
        builder.HasIndex(e => new { e.Provider, e.Issuer, e.Subject }).IsUnique();
        builder.HasIndex(e => e.UserId);
        builder.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class UserPasskeyConfiguration : IEntityTypeConfiguration<UserPasskey>
{
    public void Configure(EntityTypeBuilder<UserPasskey> builder)
    {
        builder.ToTable("UserPasskeys");
        builder.HasKey(e => e.Id);
        // WebAuthn allows ids of up to 1023 bytes, within SQL Server's 1700-byte limit for an index key.
        builder.Property(e => e.CredentialId).HasMaxLength(1023).IsRequired();
        builder.HasIndex(e => e.CredentialId).IsUnique();
        builder.Property(e => e.PublicKey).IsRequired();
        // Two sign-ins with one passkey at once read the same counter; only the first may move it,
        // so a copied key presenting the same value at the same moment is caught (UPDATE ... WHERE SignCount = old).
        builder.Property(e => e.SignCount).IsConcurrencyToken();
        builder.Property(e => e.Name).HasMaxLength(60).IsRequired();
        builder.Property(e => e.Transports).HasMaxLength(100);
        builder.HasIndex(e => e.UserId);
        builder.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AuthChallengeConfiguration : IEntityTypeConfiguration<AuthChallenge>
{
    public void Configure(EntityTypeBuilder<AuthChallenge> builder)
    {
        builder.ToTable("AuthChallenges");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Purpose).HasMaxLength(40).IsRequired();
        builder.Property(e => e.KeyHash).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Payload).IsRequired();
        builder.HasIndex(e => new { e.Purpose, e.KeyHash }).IsUnique();
        // The sweep of steps nobody finished reads the oldest first.
        builder.HasIndex(e => e.ExpiresAtUtc);
    }
}
