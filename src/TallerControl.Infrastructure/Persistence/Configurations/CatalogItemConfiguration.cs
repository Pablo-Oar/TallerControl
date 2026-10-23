using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Catalog;
using TallerControl.Domain.Tenancy;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class CatalogItemConfiguration : IEntityTypeConfiguration<CatalogItem>
{
    public void Configure(EntityTypeBuilder<CatalogItem> builder)
    {
        builder.ToTable("catalog_items");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(c => c.TenantId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(c => c.Description).HasMaxLength(CatalogItem.DescriptionMaxLength);
        builder.Property(c => c.StockMax).HasColumnType("numeric(12,3)");

        builder.Property(c => c.Name).IsRequired().HasMaxLength(CatalogItem.NameMaxLength);
        builder.Property(c => c.Sku).HasMaxLength(CatalogItem.SkuMaxLength);

        // Cantidades: numeric(12,3)
        builder.Property(c => c.StockQty).HasColumnType("numeric(12,3)");
        builder.Property(c => c.StockMin).HasColumnType("numeric(12,3)");

        // Precios: numeric(14,2) por convención global
        builder.Property(c => c.NetPrice).IsRequired();
        builder.Property(c => c.VatRate).IsRequired();
        builder.Property(c => c.Cost);

        builder.Property(c => c.TrackStock).HasDefaultValue(false);
        builder.Property(c => c.IsActive).HasDefaultValue(true);

        // IAuditable
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.CreatedBy);
        builder.Property(c => c.UpdatedAt);
        builder.Property(c => c.UpdatedBy);

        // ISoftDeletable
        builder.Property(c => c.DeletedAt);
        builder.Property(c => c.DeletedBy);
        builder.Property(c => c.DeleteReason).HasColumnName("delete_reason");

        // Índices
        builder.HasIndex(c => new { c.TenantId, c.Name });

        // SKU único por taller entre activos
        builder.HasIndex(c => new { c.TenantId, c.Sku })
            .IsUnique()
            .HasFilter("deleted_at is null and sku is not null");

        // GIN trigram para búsqueda de texto
        builder.HasIndex(c => c.Name)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops")
            .HasDatabaseName("ix_catalog_items_name_trgm");
    }
}
