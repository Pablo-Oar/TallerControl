using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Users;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", t =>
        {
            t.HasCheckConstraint("ck_users_display_name_length",
                $"char_length(display_name) BETWEEN 1 AND {User.DisplayNameMaxLength}");
            t.HasCheckConstraint("ck_users_email_not_empty", "char_length(email) > 0");
        });

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.Email)
            .HasColumnType("citext")
            .IsRequired();

        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.DisplayName).IsRequired();
        builder.Property(u => u.PasswordHash);
        builder.Property(u => u.SecurityStamp).IsRequired();
        builder.Property(u => u.Status).HasDefaultValue(UserStatus.Pending);
        builder.Property(u => u.AccessFailedCount).HasDefaultValue(0);
    }
}
