using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Catalog;
using TallerControl.Domain.CounterSales;
using TallerControl.Domain.Tenancy;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class CounterSaleItemConfiguration : IEntityTypeConfiguration<CounterSaleItem>
{
    public void Configure(EntityTypeBuilder<CounterSaleItem> builder)
    {
        builder.ToTable("counter_sale_items");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(i => i.TenantId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CatalogItem>().WithMany()
            .HasForeignKey(i => i.CatalogItemId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(i => i.Description).IsRequired().HasMaxLength(300);

        // Cantidades: numeric(12,3)
        builder.Property(i => i.Quantity).HasColumnType("numeric(12,3)").IsRequired();
        builder.Property(i => i.UnitPrice).IsRequired();
        builder.Property(i => i.VatRate).IsRequired();
        builder.Property(i => i.NetAmount).IsRequired();
        builder.Property(i => i.VatAmount).IsRequired();
        builder.Property(i => i.Total).IsRequired();
    }
}
