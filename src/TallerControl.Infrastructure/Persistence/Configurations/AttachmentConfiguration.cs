using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Work;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("attachments");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(a => a.TenantId).OnDelete(DeleteBehavior.Restrict);

        // FK polimórfica: no hay FK de BD, la aplicación valida que owner_id exista en el taller.
        builder.Property(a => a.OwnerId).IsRequired();

        builder.Property(a => a.FileKey).IsRequired().HasMaxLength(500);
        builder.Property(a => a.ContentType).IsRequired().HasMaxLength(100);
        builder.Property(a => a.SizeBytes).IsRequired();
        builder.Property(a => a.IsInternal).HasDefaultValue(false);
        builder.Property(a => a.CreatedAt).IsRequired();

        // ISoftDeletable parcial: solo deleted_at (no reason/by, ver Attachment.SoftDelete).
        builder.Property(a => a.DeletedAt);

        builder.HasIndex(a => new { a.TenantId, a.OwnerType, a.OwnerId })
            .HasDatabaseName("ix_attachments_tenant_owner");
    }
}
