using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Cash;
using TallerControl.Domain.Tenancy;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class CashAccountConfiguration : IEntityTypeConfiguration<CashAccount>
{
    public void Configure(EntityTypeBuilder<CashAccount> builder)
    {
        builder.ToTable("cash_accounts");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(a => a.TenantId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Branch>().WithMany()
            .HasForeignKey(a => a.BranchId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(a => a.Name).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Kind).IsRequired();
        builder.Property(a => a.IsActive).HasDefaultValue(true);

        // IVersioned — xmin
        builder.Property(a => a.Version).HasColumnName("xmin");

        // IAuditable
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.CreatedBy);
        builder.Property(a => a.UpdatedAt);
        builder.Property(a => a.UpdatedBy);

        // ISoftDeletable
        builder.Property(a => a.DeletedAt);
        builder.Property(a => a.DeletedBy);
        builder.Property(a => a.DeleteReason).HasColumnName("delete_reason");

        builder.HasIndex(a => new { a.TenantId, a.BranchId });
    }
}
