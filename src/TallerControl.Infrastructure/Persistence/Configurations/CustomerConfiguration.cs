using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Customers;
using TallerControl.Domain.Tenancy;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(c => c.TenantId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(c => c.Name).IsRequired().HasMaxLength(Customer.NameMaxLength);
        builder.Property(c => c.Email).HasColumnType("citext");
        builder.Property(c => c.VatCondition).IsRequired();
        builder.Property(c => c.DoNotContact).HasDefaultValue(false);
        builder.Property(c => c.OwnerAccessCodeHash).HasColumnType("bytea");

        // IAuditable — seteados por AppDbContext en SaveChanges
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
        builder.HasIndex(c => new { c.TenantId, c.Phone });

        // Unicidad de documento (parcial: excluye nulos y borrados)
        builder.HasIndex(c => new { c.TenantId, c.DocType, c.DocNumber })
            .IsUnique()
            .HasFilter("deleted_at is null and doc_number is not null");

        // Índice GIN trigram en name para búsqueda de texto (pg_trgm)
        builder.HasIndex(c => c.Name)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops")
            .HasDatabaseName("ix_customers_name_trgm");
    }
}
