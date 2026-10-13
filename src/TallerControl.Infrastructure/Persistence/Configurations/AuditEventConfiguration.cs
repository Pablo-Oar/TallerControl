using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Audit;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_events");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.EntityType).IsRequired();
        builder.Property(e => e.Action).IsRequired();
        builder.Property(e => e.ChangesJson).HasColumnName("changes").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.IpTruncated).HasColumnType("inet");

        builder.HasIndex(e => new { e.TenantId, e.EntityType, e.EntityId, e.OccurredAt })
            .IsDescending(false, false, false, true);
    }
}
