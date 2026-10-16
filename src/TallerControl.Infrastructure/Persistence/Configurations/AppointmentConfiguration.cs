using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Customers;
using TallerControl.Domain.Scheduling;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Vehicles;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("appointments");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(a => a.TenantId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Customer>().WithMany()
            .HasForeignKey(a => a.CustomerId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Vehicle>().WithMany()
            .HasForeignKey(a => a.VehicleId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(a => a.Version).HasColumnName("xmin").IsRowVersion();
        builder.Property(a => a.Status).IsRequired();
        builder.Property(a => a.ScheduledAt).IsRequired();
        builder.Property(a => a.TotalDurationMinutes).IsRequired();
        builder.Property(a => a.Notes).HasMaxLength(1000);
        builder.Property(a => a.CancellationReason).HasMaxLength(500);
        builder.Property(a => a.ConfirmationTokenHash).HasColumnType("bytea");

        // IAuditable
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.CreatedBy);
        builder.Property(a => a.UpdatedAt);
        builder.Property(a => a.UpdatedBy);

        builder.HasMany(a => a.Services)
            .WithOne()
            .HasForeignKey(s => s.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Un solo turno activo (no cancelado/completado) por vehículo en el mismo slot.
        builder.HasIndex(a => new { a.TenantId, a.VehicleId, a.ScheduledAt })
            .HasFilter("status NOT IN ('cancelled', 'completed')")
            .IsUnique();

        // Índice por token para la ruta pública de confirmación.
        builder.HasIndex(a => a.ConfirmationTokenHash)
            .HasFilter("confirmation_token_hash IS NOT NULL");
    }
}
