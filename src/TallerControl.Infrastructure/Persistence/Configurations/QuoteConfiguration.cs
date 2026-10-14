using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Customers;
using TallerControl.Domain.Quotes;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Vehicles;
using TallerControl.Domain.Work;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.ToTable("quotes");

        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).ValueGeneratedNever();

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(q => q.TenantId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Customer>().WithMany()
            .HasForeignKey(q => q.CustomerId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Vehicle>().WithMany()
            .HasForeignKey(q => q.VehicleId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<WorkOrder>().WithMany()
            .HasForeignKey(q => q.WorkOrderId).OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.Property(q => q.Number).IsRequired();
        builder.Property(q => q.GlobalDiscountPct).IsRequired().HasDefaultValue(0m);
        builder.Property(q => q.SubtotalNet).IsRequired();
        builder.Property(q => q.DiscountAmount).IsRequired();
        builder.Property(q => q.VatAmount).IsRequired();
        builder.Property(q => q.Total).IsRequired();
        builder.Property(q => q.ValidUntil).IsRequired();
        builder.Property(q => q.CustomerNotes).HasMaxLength(Quote.CustomerNotesMaxLength);
        builder.Property(q => q.InternalNotes).HasMaxLength(Quote.InternalNotesMaxLength);
        builder.Property(q => q.CancelReason).HasMaxLength(Quote.CancelReasonMaxLength);

        // IAuditable
        builder.Property(q => q.CreatedAt).IsRequired();
        builder.Property(q => q.CreatedBy);
        builder.Property(q => q.UpdatedAt);
        builder.Property(q => q.UpdatedBy);

        // ISoftDeletable
        builder.Property(q => q.DeletedAt);
        builder.Property(q => q.DeletedBy);
        builder.Property(q => q.DeleteReason).HasColumnName("delete_reason");

        // Colecciones con backing fields privados
        builder.Navigation(q => q.Items).HasField("_items");
        builder.Navigation(q => q.Responses).HasField("_responses");

        builder.HasMany(q => q.Items)
            .WithOne()
            .HasForeignKey(i => i.QuoteId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(q => q.Responses)
            .WithOne()
            .HasForeignKey(r => r.QuoteId).OnDelete(DeleteBehavior.Restrict);

        // xmin aplicado por convención (IVersioned)

        // Índices
        builder.HasIndex(q => new { q.TenantId, q.Number })
            .IsUnique()
            .HasDatabaseName("ix_quotes_tenant_number");

        builder.HasIndex(q => new { q.TenantId, q.Status })
            .HasDatabaseName("ix_quotes_tenant_status");

        builder.HasIndex(q => new { q.TenantId, q.CustomerId })
            .HasDatabaseName("ix_quotes_tenant_customer");

        builder.HasIndex(q => new { q.TenantId, q.VehicleId })
            .HasDatabaseName("ix_quotes_tenant_vehicle");
    }
}
