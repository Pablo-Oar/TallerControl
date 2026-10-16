using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Scheduling;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Users;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class AvailabilityRuleConfiguration : IEntityTypeConfiguration<AvailabilityRule>
{
    public void Configure(EntityTypeBuilder<AvailabilityRule> builder)
    {
        builder.ToTable("availability_rules");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(r => r.TenantId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Membership>().WithMany()
            .HasForeignKey(r => r.MechanicMembershipId).OnDelete(DeleteBehavior.Cascade);

        builder.Property(r => r.DayOfWeek).IsRequired()
            .HasConversion<int>();

        builder.Property(r => r.StartTime).IsRequired()
            .HasColumnType("time");

        builder.Property(r => r.EndTime).IsRequired()
            .HasColumnType("time");

        // IAuditable
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.CreatedBy);
        builder.Property(r => r.UpdatedAt);
        builder.Property(r => r.UpdatedBy);

        // Un mecánico no puede tener dos reglas para el mismo día de la semana.
        builder.HasIndex(r => new { r.TenantId, r.MechanicMembershipId, r.DayOfWeek }).IsUnique();
    }
}
