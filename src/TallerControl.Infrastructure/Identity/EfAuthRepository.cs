using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Auth;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Users;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Identity;

/// <summary>
/// Implementación EF Core de <see cref="IAuthRepository"/>. Ignora los filtros globales por taller
/// porque en el login el taller aún no está resuelto.
/// </summary>
internal sealed class EfAuthRepository : IAuthRepository
{
    private readonly AppDbContext _db;

    public EfAuthRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<User?> FindUserByEmailAsync(string email, CancellationToken ct) =>
        _db.Set<User>().FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> FindUserByIdAsync(Guid userId, CancellationToken ct) =>
        _db.Set<User>().FirstOrDefaultAsync(u => u.Id == userId, ct);

    public async Task<MembershipWithTenant?> FindActiveMembershipAsync(Guid userId, CancellationToken ct)
    {
        var membership = await _db.Set<Membership>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.UserId == userId && m.Status == MembershipStatus.Active, ct);

        if (membership is null)
        {
            return null;
        }

        var tenant = await _db.Set<Tenant>()
            .FirstOrDefaultAsync(t => t.Id == membership.TenantId, ct);

        if (tenant is null)
        {
            return null;
        }

        var settings = await _db.Set<TenantSettings>().IgnoreQueryFilters().AsNoTracking()
            .Where(s => s.TenantId == tenant.Id)
            .Select(s => new { s.DefaultVat, s.QuoteValidityDays })
            .FirstOrDefaultAsync(ct);

        return new MembershipWithTenant(
            membership, tenant,
            settings?.DefaultVat ?? TenantSettings.DefaultVatRate,
            settings?.QuoteValidityDays ?? TenantSettings.DefaultQuoteValidityDays);
    }

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
