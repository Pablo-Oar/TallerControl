using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Suppliers;
using TallerControl.Domain.Tenancy;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("suppliers");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Cuit).HasMaxLength(20);
        builder.Property(s => s.VatCondition).IsRequired();
        builder.Property(s => s.Address).HasMaxLength(300);
        builder.Property(s => s.Phone).HasMaxLength(50);
        builder.Property(s => s.Email).HasMaxLength(200);
        builder.Property(s => s.Notes).HasMaxLength(1000);

        // IVersioned — xmin
        builder.Property(s => s.Version).HasColumnName("xmin");

        // IAuditable
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.CreatedBy);
        builder.Property(s => s.UpdatedAt);
        builder.Property(s => s.UpdatedBy);

        // ISoftDeletable
        builder.Property(s => s.DeletedAt);
        builder.Property(s => s.DeletedBy);
        builder.Property(s => s.DeleteReason).HasColumnName("delete_reason");

        builder.HasIndex(s => new { s.TenantId, s.Name });

        // GIN trigram para búsqueda
        builder.HasIndex(s => s.Name)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops")
            .HasDatabaseName("ix_suppliers_name_trgm");
    }
}
