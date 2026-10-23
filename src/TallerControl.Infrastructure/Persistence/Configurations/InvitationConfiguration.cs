using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Users;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("invitations");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(i => i.TenantId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(i => i.Email)
            .HasColumnType("citext")
            .IsRequired();

        builder.Property(i => i.Role).IsRequired();

        builder.Property(i => i.Permissions)
            .HasColumnName("permissions")
            .HasColumnType("text[]");

        builder.Property(i => i.TokenHash).IsRequired();

        builder.HasIndex(i => i.TokenHash).IsUnique();

        builder.Property(i => i.ExpiresAt).IsRequired();
        builder.Property(i => i.DisplayName).HasMaxLength(200);
    }
}
