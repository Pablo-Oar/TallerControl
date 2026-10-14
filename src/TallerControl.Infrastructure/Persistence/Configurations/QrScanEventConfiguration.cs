using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Qr;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class QrScanEventConfiguration : IEntityTypeConfiguration<QrScanEvent>
{
    public void Configure(EntityTypeBuilder<QrScanEvent> builder)
    {
        builder.ToTable("qr_scan_events");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        // TenantId nullable — puede ser null si el código no existe en ningún taller
        builder.Property(e => e.TenantId);
        builder.Property(e => e.QrCardId);
        builder.Property(e => e.CodeHash).IsRequired().HasColumnType("bytea");
        builder.Property(e => e.EventType).IsRequired();
        builder.Property(e => e.Outcome).HasMaxLength(50);
        builder.Property(e => e.IpTruncated).HasMaxLength(50);
        builder.Property(e => e.UserAgent).HasMaxLength(200);
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.QrCardId, e.CreatedAt })
            .HasDatabaseName("ix_qr_scan_events_tenant_card_at");
    }
}
