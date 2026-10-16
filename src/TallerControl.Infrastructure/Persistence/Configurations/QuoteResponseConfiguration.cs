using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Quotes;
using TallerControl.Domain.Tenancy;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class QuoteResponseConfiguration : IEntityTypeConfiguration<QuoteResponse>
{
    public void Configure(EntityTypeBuilder<QuoteResponse> builder)
    {
        builder.ToTable("quote_responses");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(r => r.TenantId).OnDelete(DeleteBehavior.Restrict);

        // Append-only: el rol app no puede UPDATE ni DELETE (se revoca en la migración).
        builder.Property(r => r.Decision).IsRequired().HasMaxLength(20);
        builder.Property(r => r.SignerName).IsRequired().HasMaxLength(QuoteResponse.SignerNameMaxLength);
        builder.Property(r => r.ApprovedTotal).IsRequired();
        builder.Property(r => r.ContentHash).IsRequired().HasColumnType("bytea");
        builder.Property(r => r.UserAgent).HasMaxLength(500);
        builder.Property(r => r.Comment).HasMaxLength(QuoteResponse.CommentMaxLength);

        // Firma digital y DNI cifrado (RF-R4, F-16 — P1, agregado en E13 bloque 4).
        builder.Property(r => r.SignatureFileKey).HasMaxLength(500);
        builder.Property(r => r.SignerDocNumberEnc).HasColumnType("bytea");
        builder.Property(r => r.IsCurrent).IsRequired().HasDefaultValue(true);
        builder.Property(r => r.CreatedAt).IsRequired();

        // ClientIpTruncated como inet de PostgreSQL.
        builder.Property(r => r.ClientIpTruncated).HasColumnType("inet");

        // Colección de ítems con backing field
        builder.Navigation(r => r.Items).HasField("_items");

        builder.HasMany(r => r.Items)
            .WithOne()
            .HasForeignKey(ri => ri.ResponseId).OnDelete(DeleteBehavior.Restrict);

        // Índice único parcial: solo una respuesta is_current por presupuesto
        builder.HasIndex(r => new { r.TenantId, r.QuoteId })
            .HasFilter("is_current = true")
            .IsUnique()
            .HasDatabaseName("ix_quote_responses_current");

        builder.HasIndex(r => new { r.TenantId, r.QuoteId })
            .HasDatabaseName("ix_quote_responses_tenant_quote");
    }
}
