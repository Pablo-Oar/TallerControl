using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Catalog;
using TallerControl.Domain.Purchases;
using TallerControl.Domain.Tenancy;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class PurchaseItemConfiguration : IEntityTypeConfiguration<PurchaseItem>
{
    public void Configure(EntityTypeBuilder<PurchaseItem> builder)
    {
        builder.ToTable("purchase_items");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(i => i.TenantId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CatalogItem>().WithMany()
            .HasForeignKey(i => i.CatalogItemId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(i => i.Description).IsRequired().HasMaxLength(300);

        // Cantidades: numeric(12,3)
        builder.Property(i => i.Quantity).HasColumnType("numeric(12,3)").IsRequired();
        builder.Property(i => i.UnitCost).IsRequired();
        builder.Property(i => i.VatRate).IsRequired();
        builder.Property(i => i.Total).IsRequired();
    }
}
