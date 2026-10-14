using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TallerControl.Domain.Quotes;

namespace TallerControl.Infrastructure.Persistence.Configurations;

internal sealed class QuoteResponseItemConfiguration : IEntityTypeConfiguration<QuoteResponseItem>
{
    public void Configure(EntityTypeBuilder<QuoteResponseItem> builder)
    {
        builder.ToTable("quote_response_items");

        // PK compuesta
        builder.HasKey(ri => new { ri.ResponseId, ri.QuoteItemId });

        builder.HasOne<QuoteItem>().WithMany()
            .HasForeignKey(ri => ri.QuoteItemId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(ri => ri.Approved).IsRequired();
    }
}
