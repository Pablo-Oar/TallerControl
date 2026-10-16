using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Customers;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Vehicles;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("vehicles", t =>
            t.HasCheckConstraint("ck_vehicles_year_range", "year IS NULL OR (year BETWEEN 1900 AND extract(year from now()) + 1)"));

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(v => v.TenantId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Customer>().WithMany()
            .HasForeignKey(v => v.CustomerId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(v => v.Plate).IsRequired();

        // Vehicle.Version (IVersioned) → xmin explícito para que no colisione con VersionName
        builder.Property(v => v.Version).HasColumnName("xmin");
        // VersionName → columna "version" del modelo (doc 04 §3.2)
        builder.Property(v => v.VersionName).HasColumnName("version");

        builder.Property(v => v.OdometerKm).IsRequired().HasDefaultValue(0);
        builder.Property(v => v.Year).HasColumnType("smallint");
        builder.Property(v => v.EngineNumber).HasColumnName("engine_number");

        // IAuditable
        builder.Property(v => v.CreatedAt).IsRequired();
        builder.Property(v => v.CreatedBy);
        builder.Property(v => v.UpdatedAt);
        builder.Property(v => v.UpdatedBy);

        // ISoftDeletable
        builder.Property(v => v.DeletedAt);
        builder.Property(v => v.DeletedBy);
        builder.Property(v => v.DeleteReason).HasColumnName("delete_reason");

        // Patente única por taller entre vehículos activos (REQ-06)
        builder.HasIndex(v => new { v.TenantId, v.Plate })
            .IsUnique()
            .HasFilter("deleted_at is null");

        // VIN único por taller (parcial)
        builder.HasIndex(v => new { v.TenantId, v.Vin })
            .IsUnique()
            .HasFilter("deleted_at is null and vin is not null");

        builder.HasIndex(v => new { v.TenantId, v.CustomerId });
    }
}
