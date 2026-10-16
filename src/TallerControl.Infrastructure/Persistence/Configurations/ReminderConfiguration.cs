using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Reminders;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Vehicles;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class ReminderConfiguration : IEntityTypeConfiguration<Reminder>
{
    public void Configure(EntityTypeBuilder<Reminder> builder)
    {
        builder.ToTable("reminders");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(r => r.TenantId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Vehicle>().WithMany()
            .HasForeignKey(r => r.VehicleId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(r => r.Version).HasColumnName("xmin");
        builder.Property(r => r.Kind).IsRequired();
        builder.Property(r => r.Title).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Notes).HasMaxLength(1000);

        // IAuditable
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.CreatedBy);
        builder.Property(r => r.UpdatedAt);
        builder.Property(r => r.UpdatedBy);

        // ISoftDeletable
        builder.Property(r => r.DeletedAt);
        builder.Property(r => r.DeletedBy);
        builder.Property(r => r.DeleteReason).HasColumnName("delete_reason");

        builder.HasIndex(r => new { r.TenantId, r.VehicleId })
            .HasFilter("deleted_at is null");

        builder.HasIndex(r => new { r.TenantId, r.DueDate })
            .HasFilter("deleted_at is null and due_date is not null");
    }
}
