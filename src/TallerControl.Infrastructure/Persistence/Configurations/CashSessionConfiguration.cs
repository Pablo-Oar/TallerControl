using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Cash;
using TallerControl.Domain.Tenancy;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class CashSessionConfiguration : IEntityTypeConfiguration<CashSession>
{
    public void Configure(EntityTypeBuilder<CashSession> builder)
    {
        builder.ToTable("cash_sessions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Branch>().WithMany()
            .HasForeignKey(s => s.BranchId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CashAccount>().WithMany()
            .HasForeignKey(s => s.AccountId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(s => s.Status).IsRequired();
        builder.Property(s => s.OpenedAt).IsRequired();
        builder.Property(s => s.OpeningNote).HasMaxLength(500);
        builder.Property(s => s.ClosingNote).HasMaxLength(500);

        // IAuditable
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.CreatedBy);
        builder.Property(s => s.UpdatedAt);
        builder.Property(s => s.UpdatedBy);

        builder.HasIndex(s => new { s.TenantId, s.AccountId, s.Status });
        builder.HasQueryFilter(s => s.TenantId == EF.Property<Guid>(s, "TenantId"));
    }
}
