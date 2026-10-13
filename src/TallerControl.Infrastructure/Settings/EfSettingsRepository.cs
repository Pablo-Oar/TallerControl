using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Common;
using TallerControl.Application.Settings;
using TallerControl.Domain.Tenancy;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Settings;

internal sealed class EfSettingsRepository : ISettingsRepository
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public EfSettingsRepository(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public Task<Tenant?> FindTenantAsync(CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId is null)
        {
            return Task.FromResult<Tenant?>(null);
        }

        return _db.Set<Tenant>().FirstOrDefaultAsync(t => t.Id == tenantId.Value, ct);
    }

    public Task<TenantSettings?> FindSettingsAsync(CancellationToken ct) =>
        _db.Set<TenantSettings>().FirstOrDefaultAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
