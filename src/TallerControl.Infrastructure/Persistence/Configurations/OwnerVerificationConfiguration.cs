using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Qr;
using TallerControl.Domain.Tenancy;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class OwnerVerificationConfiguration : IEntityTypeConfiguration<OwnerVerification>
{
    public void Configure(EntityTypeBuilder<OwnerVerification> builder)
    {
        builder.ToTable("owner_verifications");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(v => v.TenantId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(v => v.QrCardId).IsRequired();
        builder.Property(v => v.CustomerId).IsRequired();
        builder.Property(v => v.OtpHash).IsRequired().HasColumnType("bytea");
        builder.Property(v => v.ExpiresAt).IsRequired();
        builder.Property(v => v.Attempts).IsRequired().HasDefaultValue(0);
        builder.Property(v => v.ConsumedAt);

        // Búsqueda por challenge ID (la PK); índice por tarjeta+expiración para invalidar
        builder.HasIndex(v => new { v.QrCardId, v.ExpiresAt })
            .HasDatabaseName("ix_owner_verifications_card_expires");
    }
}
