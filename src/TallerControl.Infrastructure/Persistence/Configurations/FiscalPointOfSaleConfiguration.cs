using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Invoicing;
using TallerControl.Domain.Tenancy;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class FiscalPointOfSaleConfiguration : IEntityTypeConfiguration<FiscalPointOfSale>
{
    public void Configure(EntityTypeBuilder<FiscalPointOfSale> builder)
    {
        builder.ToTable("fiscal_points_of_sale");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(p => p.TenantId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.FiscalCertificate).WithMany()
            .HasForeignKey(p => p.FiscalCertificateId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(FiscalPointOfSale.NameMaxLength);

        // Número único por taller
        builder.HasIndex(p => new { p.TenantId, p.Number })
            .IsUnique()
            .HasDatabaseName("ix_fiscal_points_of_sale_tenant_number");

        // IVersioned — xmin
        builder.Property(p => p.Version).HasColumnName("xmin");
    }
}
