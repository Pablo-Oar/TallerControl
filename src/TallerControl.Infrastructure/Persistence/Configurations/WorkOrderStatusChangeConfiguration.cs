using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Work;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class WorkOrderStatusChangeConfiguration : IEntityTypeConfiguration<WorkOrderStatusChange>
{
    public void Configure(EntityTypeBuilder<WorkOrderStatusChange> builder)
    {
        builder.ToTable("work_order_status_changes");

        builder.HasKey(sc => sc.Id);
        builder.Property(sc => sc.Id).ValueGeneratedNever();

        // Append-only: el rol app no puede UPDATE ni DELETE (se revoca en la migración).
        builder.Property(sc => sc.ChangedAt).IsRequired();

        builder.HasIndex(sc => new { sc.TenantId, sc.WorkOrderId })
            .HasDatabaseName("ix_work_order_status_changes_tenant_order");
    }
}
