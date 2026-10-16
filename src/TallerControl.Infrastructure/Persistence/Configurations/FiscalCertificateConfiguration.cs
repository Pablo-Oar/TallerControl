using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Invoicing;
using TallerControl.Domain.Tenancy;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class FiscalCertificateConfiguration : IEntityTypeConfiguration<FiscalCertificate>
{
    public void Configure(EntityTypeBuilder<FiscalCertificate> builder)
    {
        builder.ToTable("fiscal_certificates");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(c => c.TenantId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(c => c.Cuit)
            .IsRequired()
            .HasMaxLength(FiscalCertificate.CuitNormalizedLength);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(FiscalCertificate.NameMaxLength);

        builder.Property(c => c.PrivateKeyEncrypted).IsRequired();
        builder.Property(c => c.CsrPem).IsRequired().HasMaxLength(FiscalCertificate.CsrMaxLength);
        builder.Property(c => c.CertificatePem).HasMaxLength(FiscalCertificate.CertMaxLength);

        builder.HasIndex(c => new { c.TenantId, c.Cuit })
            .IsUnique()
            .HasDatabaseName("ix_fiscal_certificates_tenant_cuit");

        builder.HasIndex(c => c.TenantId)
            .HasFilter("status = 'active'")
            .HasDatabaseName("ix_fiscal_certificates_tenant_active");

        // IVersioned — xmin
        builder.Property(c => c.Version).HasColumnName("xmin");
    }
}
