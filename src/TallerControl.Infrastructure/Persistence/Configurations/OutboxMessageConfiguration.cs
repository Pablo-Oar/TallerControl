using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Outbox;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages", t =>
            t.HasCheckConstraint("ck_outbox_messages_attempts", "attempts >= 0"));

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Type).IsRequired();
        builder.Property(m => m.PayloadJson).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(m => m.Attempts).HasDefaultValue(0);

        // El despachador busca solo lo pendiente.
        builder.HasIndex(m => m.CreatedAt).HasFilter("processed_at IS NULL");
    }
}
