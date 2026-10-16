using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Invoicing;
using TallerControl.Domain.Tenancy;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(i => i.TenantId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.FiscalPointOfSale).WithMany()
            .HasForeignKey(i => i.FiscalPointOfSaleId).OnDelete(DeleteBehavior.Restrict);

        // Nota de crédito referencia a la factura original (nullable FK)
        builder.HasOne<Invoice>().WithMany()
            .HasForeignKey(i => i.OriginalInvoiceId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(i => i.Cae).HasMaxLength(Invoice.CaeMaxLength);
        builder.Property(i => i.RejectionReason).HasMaxLength(Invoice.RejectionReasonMaxLength);
        builder.Property(i => i.ArcaResponse).HasMaxLength(Invoice.ArcaResponseMaxLength);

        // numeric(14,2) aplicado por convención global en AppDbContext
        builder.Property(i => i.NetAmount).IsRequired();
        builder.Property(i => i.VatAmount).IsRequired();
        builder.Property(i => i.TotalAmount).IsRequired();

        // Índice para buscar la factura de una OT/VentaMostrador
        builder.HasIndex(i => new { i.TenantId, i.SourceType, i.SourceId })
            .HasDatabaseName("ix_invoices_tenant_source");

        // Índice por período de emisión
        builder.HasIndex(i => new { i.TenantId, i.IssuedAt })
            .HasDatabaseName("ix_invoices_tenant_issued_at");

        // IVersioned — xmin
        builder.Property(i => i.Version).HasColumnName("xmin");
    }
}
