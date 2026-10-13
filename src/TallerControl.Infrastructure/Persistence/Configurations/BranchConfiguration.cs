using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Tenancy;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("branches", t =>
            t.HasCheckConstraint("ck_branches_name_length", "char_length(name) BETWEEN 1 AND 120"));

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.Name).IsRequired();

        builder.HasOne<Tenant>().WithMany().HasForeignKey(b => b.TenantId).OnDelete(DeleteBehavior.Restrict);

        // Permite que document_sequences referencie (tenant_id, branch_id) y no mezcle talleres.
        builder.HasAlternateKey(b => new { b.TenantId, b.Id });

        builder.HasIndex(b => new { b.TenantId, b.Name }).IsUnique();

        // Una sola sucursal principal por taller.
        builder.HasIndex(b => b.TenantId).IsUnique().HasFilter("is_main");
    }
}
