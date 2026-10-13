using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Tenancy;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants", t =>
            t.HasCheckConstraint("ck_tenants_name_length", "char_length(name) BETWEEN 1 AND 120"));

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Name).IsRequired();
        builder.Property(t => t.TimeZone).IsRequired().HasDefaultValue(Tenant.DefaultTimeZone);
        builder.Property(t => t.Status).HasDefaultValue(TenantStatus.Active);
    }
}
