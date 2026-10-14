using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Customers;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Users;
using TallerControl.Domain.Vehicles;
using TallerControl.Domain.Work;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.ToTable("work_orders");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).ValueGeneratedNever();

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(w => w.TenantId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Branch>().WithMany()
            .HasForeignKey(w => w.BranchId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Customer>().WithMany()
            .HasForeignKey(w => w.CustomerId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Vehicle>().WithMany()
            .HasForeignKey(w => w.VehicleId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Membership>().WithMany()
            .HasForeignKey(w => w.AssignedMechanicId).OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.Property(w => w.Number).IsRequired();
        builder.Property(w => w.OdometerKm).IsRequired();
        builder.Property(w => w.ReportedIssue).HasMaxLength(WorkOrder.ReportedIssueMaxLength);
        builder.Property(w => w.MechanicNotes).HasMaxLength(WorkOrder.NotesMaxLength);
        builder.Property(w => w.InternalNotes).HasMaxLength(WorkOrder.NotesMaxLength);
        builder.Property(w => w.CancelReason).HasMaxLength(WorkOrder.CancelReasonMaxLength);

        // Totales dinero: numeric(14,2) viene de la convención global de AppDbContext.
        builder.Property(w => w.SubtotalNet).IsRequired();
        builder.Property(w => w.DiscountAmount).IsRequired();
        builder.Property(w => w.VatAmount).IsRequired();
        builder.Property(w => w.Total).IsRequired();
        builder.Property(w => w.PaidAmount).IsRequired();

        // balance = CASE WHEN status = 'cancelled' THEN 0 ELSE total - paid_amount END (generada en BD).
        // EF la registra como computed para que no la escriba y sí la lea.
        builder.Property<decimal>("Balance")
            .HasColumnName("balance")
            .HasComputedColumnSql(
                "CASE WHEN status = 'cancelled' THEN 0::numeric ELSE total - paid_amount END",
                stored: true);

        builder.Property(w => w.CreatedAt).IsRequired();
        builder.Property(w => w.UpdatedAt);

        // Las colecciones tienen backing fields privados (_items, _statusChanges).
        // EF los encuentra por convención de nombres ("_" + propertyName camelCase).
        builder.Navigation(w => w.Items).HasField("_items");
        builder.Navigation(w => w.StatusChanges).HasField("_statusChanges");

        builder.HasMany(w => w.Items)
            .WithOne()
            .HasForeignKey(i => i.WorkOrderId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(w => w.StatusChanges)
            .WithOne()
            .HasForeignKey(sc => sc.WorkOrderId).OnDelete(DeleteBehavior.Restrict);

        // xmin se aplica via convención en AppDbContext (IVersioned).

        // Índices
        builder.HasIndex(w => new { w.TenantId, w.BranchId, w.Number })
            .IsUnique()
            .HasDatabaseName("ix_work_orders_tenant_branch_number");

        builder.HasIndex(w => new { w.TenantId, w.Status })
            .HasDatabaseName("ix_work_orders_tenant_status");

        builder.HasIndex(w => new { w.TenantId, w.AssignedMechanicId })
            .HasDatabaseName("ix_work_orders_tenant_mechanic");

        builder.HasIndex(w => new { w.TenantId, w.VehicleId })
            .HasDatabaseName("ix_work_orders_tenant_vehicle");

        builder.HasIndex(w => new { w.TenantId, w.CustomerId })
            .HasDatabaseName("ix_work_orders_tenant_customer");
    }
}
