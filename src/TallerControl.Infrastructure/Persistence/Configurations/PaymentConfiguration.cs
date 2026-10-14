using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Payments;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Work;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(p => p.TenantId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<WorkOrder>().WithMany()
            .HasForeignKey(p => p.WorkOrderId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(p => p.Number).IsRequired();
        builder.Property(p => p.Reference).HasMaxLength(Payment.ReferenceMaxLength);
        builder.Property(p => p.VoidReason).HasMaxLength(Payment.VoidReasonMaxLength);
        builder.Property(p => p.IdempotencyKey).HasMaxLength(Payment.IdempotencyKeyMaxLength);

        // numeric(14,2) aplicado por convención global en AppDbContext.
        builder.Property(p => p.Amount).IsRequired();

        builder.Property(p => p.PublicTokenHash).IsRequired();
        builder.Property(p => p.PaidAt).IsRequired();

        // D-16: account_id nulo hasta E11 (módulo de caja).
        builder.Property(p => p.AccountId).IsRequired(false);

        // Índice único por número dentro del taller.
        builder.HasIndex(p => new { p.TenantId, p.Number })
            .IsUnique()
            .HasDatabaseName("ix_payments_tenant_number");

        // Índice único parcial para idempotencia (solo filas con clave no nula).
        builder.HasIndex(p => new { p.TenantId, p.IdempotencyKey })
            .IsUnique()
            .HasFilter("idempotency_key IS NOT NULL")
            .HasDatabaseName("ix_payments_tenant_idempotency_key");

        // Índice para búsqueda por token hash.
        builder.HasIndex(p => p.PublicTokenHash)
            .HasDatabaseName("ix_payments_public_token_hash");

        builder.HasIndex(p => new { p.TenantId, p.WorkOrderId })
            .HasDatabaseName("ix_payments_tenant_work_order");
    }
}
