using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Catalog;
using TallerControl.Domain.Work;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class WorkOrderItemConfiguration : IEntityTypeConfiguration<WorkOrderItem>
{
    public void Configure(EntityTypeBuilder<WorkOrderItem> builder)
    {
        builder.ToTable("work_order_items");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.HasOne<CatalogItem>().WithMany()
            .HasForeignKey(i => i.CatalogItemId).OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.Property(i => i.Description).IsRequired().HasMaxLength(200);

        // Cantidades: numeric(12,3)
        builder.Property(i => i.Quantity).HasColumnType("numeric(12,3)").IsRequired();

        // Precios: numeric(14,2) via convención global
        builder.Property(i => i.UnitNetPrice).IsRequired();
        builder.Property(i => i.VatRate).IsRequired();
        builder.Property(i => i.LineNet).IsRequired();
        builder.Property(i => i.LineVat).IsRequired();
        builder.Property(i => i.LineTotal).IsRequired();
        builder.Property(i => i.UnitCostSnapshot);

        builder.Property(i => i.Position).IsRequired();

        builder.HasIndex(i => new { i.TenantId, i.WorkOrderId })
            .HasDatabaseName("ix_work_order_items_tenant_order");
    }
}
