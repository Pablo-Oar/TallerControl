using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Onboarding;
using TallerControl.Domain.Catalog;
using TallerControl.Domain.Customers;
using TallerControl.Domain.Payments;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Vehicles;
using TallerControl.Domain.Work;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Onboarding;

internal sealed class EfOnboardingRepository : IOnboardingRepository
{
    private readonly AppDbContext _db;

    public EfOnboardingRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<bool> HasCustomersAsync(CancellationToken ct) =>
        _db.Set<Customer>().AsNoTracking().AnyAsync(ct);

    public Task<bool> HasVehiclesAsync(CancellationToken ct) =>
        _db.Set<Vehicle>().AsNoTracking().AnyAsync(ct);

    public Task<bool> HasCatalogItemsAsync(CancellationToken ct) =>
        _db.Set<CatalogItem>().AsNoTracking().AnyAsync(ct);

    public Task<bool> HasWorkOrdersAsync(CancellationToken ct) =>
        _db.Set<WorkOrder>().AsNoTracking().AnyAsync(ct);

    public Task<bool> HasPaymentsAsync(CancellationToken ct) =>
        _db.Set<Payment>().AsNoTracking().AnyAsync(ct);

    public Task<bool> HasLogoAsync(CancellationToken ct) =>
        _db.Set<TenantSettings>().AsNoTracking()
            .AnyAsync(s => s.LogoFileKey != null, ct);
}
