using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.PublicLinks;
using TallerControl.Domain.Tenancy;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class PublicLinkConfiguration : IEntityTypeConfiguration<PublicLink>
{
    public void Configure(EntityTypeBuilder<PublicLink> builder)
    {
        builder.ToTable("public_links");

        builder.HasKey(pl => pl.Id);
        builder.Property(pl => pl.Id).ValueGeneratedNever();

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(pl => pl.TenantId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(pl => pl.TokenHash).IsRequired().HasColumnType("bytea");
        builder.Property(pl => pl.ExpiresAt);
        builder.Property(pl => pl.RevokedAt);
        builder.Property(pl => pl.RevokedBy);
        builder.Property(pl => pl.LastAccessedAt);
        builder.Property(pl => pl.AccessCount).IsRequired().HasDefaultValue(0);

        // IAuditable
        builder.Property(pl => pl.CreatedAt).IsRequired();
        builder.Property(pl => pl.CreatedBy);
        builder.Property(pl => pl.UpdatedAt);
        builder.Property(pl => pl.UpdatedBy);

        // Búsqueda pública por hash: debe ser único (un token → un enlace).
        builder.HasIndex(pl => pl.TokenHash)
            .IsUnique()
            .HasDatabaseName("ix_public_links_token_hash");

        // Listado por taller + destino (para revocar todos los links de un presupuesto).
        builder.HasIndex(pl => new { pl.TenantId, pl.TargetType, pl.TargetId })
            .HasDatabaseName("ix_public_links_tenant_target");
    }
}
