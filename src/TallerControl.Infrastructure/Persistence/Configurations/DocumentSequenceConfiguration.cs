using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Tenancy;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class DocumentSequenceConfiguration : IEntityTypeConfiguration<DocumentSequence>
{
    public void Configure(EntityTypeBuilder<DocumentSequence> builder)
    {
        builder.ToTable("document_sequences", t =>
            t.HasCheckConstraint("ck_document_sequences_next_value", "next_value >= 1"));

        // V2 cambio 1: la clave incluye la sucursal.
        builder.HasKey(s => new { s.TenantId, s.BranchId, s.Kind });
        builder.Property(s => s.NextValue).HasDefaultValue(1);

        builder.HasOne<Branch>().WithMany()
            .HasForeignKey(s => new { s.TenantId, s.BranchId })
            .HasPrincipalKey(b => new { b.TenantId, b.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
