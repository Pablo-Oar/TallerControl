using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Purchases;
using TallerControl.Domain.Suppliers;
using TallerControl.Domain.Tenancy;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> builder)
    {
        builder.ToTable("purchases");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(p => p.TenantId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Branch>().WithMany()
            .HasForeignKey(p => p.BranchId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Supplier>().WithMany()
            .HasForeignKey(p => p.SupplierId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(p => p.DocumentNumber).IsRequired();
        builder.Property(p => p.InvoiceDate).IsRequired();
        builder.Property(p => p.Status).IsRequired();

        // D-19: campos CITI — nullable excepto perceptions/retentions (default 0)
        builder.Property(p => p.Perceptions).IsRequired().HasDefaultValue(0m);
        builder.Property(p => p.Retentions).IsRequired().HasDefaultValue(0m);

        // IVersioned — xmin
        builder.Property(p => p.Version).HasColumnName("xmin");

        // IAuditable
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.CreatedBy);
        builder.Property(p => p.UpdatedAt);
        builder.Property(p => p.UpdatedBy);

        // ISoftDeletable
        builder.Property(p => p.DeletedAt);
        builder.Property(p => p.DeletedBy);
        builder.Property(p => p.DeleteReason).HasColumnName("delete_reason");

        builder.HasMany(p => p.Items)
            .WithOne()
            .HasForeignKey(i => i.PurchaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.TenantId, p.BranchId, p.DocumentNumber });
        builder.HasIndex(p => new { p.TenantId, p.InvoiceDate });
    }
}
