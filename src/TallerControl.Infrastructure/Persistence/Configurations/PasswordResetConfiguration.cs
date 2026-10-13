using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Users;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class PasswordResetConfiguration : IEntityTypeConfiguration<PasswordReset>
{
    public void Configure(EntityTypeBuilder<PasswordReset> builder)
    {
        builder.ToTable("password_resets");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Email)
            .HasColumnType("citext")
            .IsRequired();

        builder.Property(r => r.TokenHash).IsRequired();

        builder.HasIndex(r => r.TokenHash).IsUnique();

        builder.Property(r => r.ExpiresAt).IsRequired();
    }
}
