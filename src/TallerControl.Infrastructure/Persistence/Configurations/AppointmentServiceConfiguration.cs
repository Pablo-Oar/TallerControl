using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Catalog;
using TallerControl.Domain.Scheduling;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class AppointmentServiceConfiguration : IEntityTypeConfiguration<AppointmentService>
{
    public void Configure(EntityTypeBuilder<AppointmentService> builder)
    {
        builder.ToTable("appointment_services");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.HasOne<CatalogItem>().WithMany()
            .HasForeignKey(s => s.CatalogItemId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(s => s.DurationMinutes).IsRequired();
        builder.Property(s => s.BufferMinutes).IsRequired();

        // TotalBlockMinutes es calculado en memoria; no se persiste.
        builder.Ignore(s => s.TotalBlockMinutes);
    }
}
