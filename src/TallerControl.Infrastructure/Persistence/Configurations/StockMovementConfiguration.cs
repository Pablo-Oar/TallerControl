using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Catalog;
using TallerControl.Domain.Tenancy;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("stock_movements");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Restrict);

        // branch_id requerido desde MVP (V2: stock por sucursal)
        builder.HasIndex(s => new { s.TenantId, s.BranchId, s.CatalogItemId });

        builder.HasOne<CatalogItem>().WithMany()
            .HasForeignKey(s => s.CatalogItemId).OnDelete(DeleteBehavior.Restrict);

        // Cantidades: numeric(12,3)
        builder.Property(s => s.Quantity)
            .IsRequired()
            .HasColumnType("numeric(12,3)");

        builder.Property(s => s.Reason).IsRequired();
        builder.Property(s => s.Note).HasMaxLength(500);

        // WorkOrderItemId es FK a work_order_items (E4+), nulo en ajustes manuales
        builder.Property(s => s.WorkOrderItemId);
        builder.Property(s => s.CreatedBy);
        builder.Property(s => s.CreatedAt).IsRequired();
    }
}
