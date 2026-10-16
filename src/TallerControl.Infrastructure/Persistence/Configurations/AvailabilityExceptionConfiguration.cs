using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Scheduling;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Users;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class AvailabilityExceptionConfiguration : IEntityTypeConfiguration<AvailabilityException>
{
    public void Configure(EntityTypeBuilder<AvailabilityException> builder)
    {
        builder.ToTable("availability_exceptions");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Membership>().WithMany()
            .HasForeignKey(e => e.MechanicMembershipId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(e => e.Date).IsRequired()
            .HasColumnType("date");

        builder.Property(e => e.IsBlocked).IsRequired();

        builder.Property(e => e.StartTime)
            .HasColumnType("time");

        builder.Property(e => e.EndTime)
            .HasColumnType("time");

        builder.Property(e => e.Reason).HasMaxLength(500);

        // IAuditable
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.CreatedBy);
        builder.Property(e => e.UpdatedAt);
        builder.Property(e => e.UpdatedBy);

        builder.HasIndex(e => new { e.TenantId, e.Date });
    }
}
