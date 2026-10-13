using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Notifications;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();
        builder.Property(n => n.Type).IsRequired();
        builder.Property(n => n.Title).IsRequired();

        // membership_id no lleva FK todavía: memberships llega con E2.
        builder.HasOne<TallerControl.Domain.Tenancy.Tenant>().WithMany()
            .HasForeignKey(n => n.TenantId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(n => new { n.TenantId, n.MembershipId, n.CreatedAt })
            .IsDescending(false, false, true);
    }
}
