using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Qr;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Vehicles;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class QrCardConfiguration : IEntityTypeConfiguration<QrCard>
{
    public void Configure(EntityTypeBuilder<QrCard> builder)
    {
        builder.ToTable("qr_cards");

        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).ValueGeneratedNever();

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(q => q.TenantId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(q => q.Code).IsRequired().HasMaxLength(10);
        builder.Property(q => q.Status).IsRequired();
        builder.Property(q => q.BatchId).IsRequired();

        builder.HasOne<Vehicle>().WithMany()
            .HasForeignKey(q => q.VehicleId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(q => q.RevokeReason).HasMaxLength(500);

        // IVersioned — xmin
        builder.Property(q => q.Version).HasColumnName("xmin");

        // IAuditable
        builder.Property(q => q.CreatedAt).IsRequired();
        builder.Property(q => q.CreatedBy);
        builder.Property(q => q.UpdatedAt);
        builder.Property(q => q.UpdatedBy);

        // Código único globalmente (no solo por taller — el código físico es irrepetible)
        builder.HasIndex(q => q.Code)
            .IsUnique()
            .HasDatabaseName("ix_qr_cards_code");

        // Un vehículo solo puede tener una tarjeta activa (assigned o lost) por taller
        builder.HasIndex(q => new { q.TenantId, q.VehicleId })
            .IsUnique()
            .HasFilter("status IN ('assigned', 'lost') AND vehicle_id IS NOT NULL")
            .HasDatabaseName("ix_qr_cards_tenant_vehicle_active");

        builder.HasIndex(q => new { q.TenantId, q.Status });
    }
}
