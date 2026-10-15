using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.CounterSales;
using TallerControl.Domain.Customers;
using TallerControl.Domain.Tenancy;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class CounterSaleConfiguration : IEntityTypeConfiguration<CounterSale>
{
    public void Configure(EntityTypeBuilder<CounterSale> builder)
    {
        builder.ToTable("counter_sales");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Branch>().WithMany()
            .HasForeignKey(s => s.BranchId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Customer>().WithMany()
            .HasForeignKey(s => s.CustomerId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(s => s.DocumentNumber).IsRequired();
        builder.Property(s => s.Date).IsRequired();
        builder.Property(s => s.Status).IsRequired();

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

        builder.HasMany(s => s.Items)
            .WithOne()
            .HasForeignKey(i => i.CounterSaleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.TenantId, s.BranchId, s.DocumentNumber });
        builder.HasIndex(s => new { s.TenantId, s.Date });
    }
}
