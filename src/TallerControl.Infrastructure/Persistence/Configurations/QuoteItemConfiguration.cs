using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Catalog;
using TallerControl.Domain.Quotes;
using TallerControl.Domain.Tenancy;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class QuoteItemConfiguration : IEntityTypeConfiguration<QuoteItem>
{
    public void Configure(EntityTypeBuilder<QuoteItem> builder)
    {
        builder.ToTable("quote_items");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(i => i.TenantId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CatalogItem>().WithMany()
            .HasForeignKey(i => i.CatalogItemId).OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.Property(i => i.Description).IsRequired().HasMaxLength(QuoteItem.DescriptionMaxLength);
        builder.Property(i => i.Quantity).HasColumnType("numeric(12,3)").IsRequired();
        builder.Property(i => i.UnitNetPrice).IsRequired();
        builder.Property(i => i.VatRate).IsRequired();
        builder.Property(i => i.UnitCostSnapshot);
        builder.Property(i => i.LineNet).IsRequired();
        builder.Property(i => i.LineVat).IsRequired();
        builder.Property(i => i.LineTotal).IsRequired();
        builder.Property(i => i.Position).IsRequired();
        builder.Property(i => i.RequiredForApproval).IsRequired().HasDefaultValue(false);

        builder.HasIndex(i => new { i.TenantId, i.QuoteId, i.Position })
            .HasDatabaseName("ix_quote_items_tenant_quote_position");
    }
}
