using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Tenancy;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class TenantSettingsConfiguration : IEntityTypeConfiguration<TenantSettings>
{
    public void Configure(EntityTypeBuilder<TenantSettings> builder)
    {
        builder.ToTable("tenant_settings", t =>
        {
            t.HasCheckConstraint("ck_tenant_settings_default_vat", "default_vat_rate IN (0, 2.5, 5, 10.5, 21, 27)");
            t.HasCheckConstraint("ck_tenant_settings_quote_validity", "quote_validity_days BETWEEN 1 AND 180");
            t.HasCheckConstraint("ck_tenant_settings_hot_quote_hours", "hot_quote_hours BETWEEN 1 AND 720");
            t.HasCheckConstraint("ck_tenant_settings_stuck_order_days", "stuck_order_days BETWEEN 1 AND 60");
            t.HasCheckConstraint("ck_tenant_settings_aged_receivable_days", "aged_receivable_days BETWEEN 1 AND 365");
        });

        builder.HasKey(s => s.TenantId);
        builder.HasOne<Tenant>().WithOne().HasForeignKey<TenantSettings>(s => s.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(s => s.DefaultVat).HasColumnName("default_vat_rate").HasPrecision(5, 2)
            .HasDefaultValue(TenantSettings.DefaultVatRate);
        builder.Property(s => s.QuoteValidityDays).HasDefaultValue(TenantSettings.DefaultQuoteValidityDays);
        builder.Property(s => s.PhoneCountryCode).IsRequired().HasDefaultValue(TenantSettings.DefaultPhoneCountryCode);
        builder.Property(s => s.QrPlateLookupEnabled).HasDefaultValue(false);
        builder.Property(s => s.HotQuoteHours).HasDefaultValue(TenantSettings.DefaultHotQuoteHours);
        builder.Property(s => s.StuckOrderDays).HasDefaultValue(TenantSettings.DefaultStuckOrderDays);
        builder.Property(s => s.AgedReceivableDays).HasDefaultValue(TenantSettings.DefaultAgedReceivableDays);
    }
}
