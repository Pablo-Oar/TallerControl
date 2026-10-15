using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Cash;
using TallerControl.Domain.Tenancy;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class CashMovementConfiguration : IEntityTypeConfiguration<CashMovement>
{
    public void Configure(EntityTypeBuilder<CashMovement> builder)
    {
        builder.ToTable("cash_movements");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(m => m.TenantId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Branch>().WithMany()
            .HasForeignKey(m => m.BranchId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CashAccount>().WithMany()
            .HasForeignKey(m => m.AccountId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(m => m.Amount).IsRequired();
        builder.Property(m => m.Direction).IsRequired();
        builder.Property(m => m.Reason).IsRequired();
        builder.Property(m => m.ReferenceType).HasMaxLength(50);
        builder.Property(m => m.Note).HasMaxLength(500);
        builder.Property(m => m.CreatedAt).IsRequired();

        builder.HasIndex(m => new { m.TenantId, m.AccountId, m.CreatedAt });

        // Append-only: el tenant puede INSERT pero no UPDATE (se aplica en la migración con REVOKE)
        builder.HasQueryFilter(m => m.TenantId == EF.Property<Guid>(m, "TenantId"));
    }
}
