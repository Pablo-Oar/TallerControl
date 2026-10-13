using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Users;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable("memberships");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(m => m.TenantId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>().WithMany()
            .HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.TenantId, m.UserId }).IsUnique();
        builder.HasIndex(m => new { m.TenantId, m.Role });

        builder.Property(m => m.Role).IsRequired();
        builder.Property(m => m.Status).HasDefaultValue(MembershipStatus.Active);

        builder.Property(m => m.Permissions)
            .HasColumnName("permissions")
            .HasColumnType("text[]");
    }
}
