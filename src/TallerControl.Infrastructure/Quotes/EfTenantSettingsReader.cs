using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Quotes;
using TallerControl.Domain.Tenancy;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Quotes;

internal sealed class EfTenantSettingsReader : ITenantSettingsReader
{
    private readonly AppDbContext _db;

    public EfTenantSettingsReader(AppDbContext db) => _db = db;

    public async Task<TenantSettingsSnapshot> GetAsync(Guid tenantId, CancellationToken ct)
    {
        var days = await _db.Set<TenantSettings>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId)
            .Select(s => s.QuoteValidityDays)
            .FirstOrDefaultAsync(ct);

        return new TenantSettingsSnapshot(days == 0 ? TenantSettings.DefaultQuoteValidityDays : days);
    }
}
