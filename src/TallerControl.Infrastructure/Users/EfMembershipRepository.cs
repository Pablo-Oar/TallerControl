using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Users;
using TallerControl.Domain.Users;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Users;

internal sealed class EfMembershipRepository : IMembershipRepository
{
    private readonly AppDbContext _db;

    public EfMembershipRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<MemberDto>> ListAsync(CancellationToken ct)
    {
        return await _db.Set<Membership>()
            .Join(
                _db.Set<User>(),
                m => m.UserId,
                u => u.Id,
                (m, u) => new MemberDto(
                    m.Id,
                    m.UserId,
                    u.DisplayName,
                    u.Email,
                    m.Role,
                    m.Permissions,
                    m.Status,
                    m.Version))
            .ToListAsync(ct);
    }

    public Task<Membership?> FindByIdAsync(Guid membershipId, CancellationToken ct) =>
        _db.Set<Membership>()
            .FirstOrDefaultAsync(m => m.Id == membershipId, ct);

    public Task<int> CountActiveOwnersAsync(CancellationToken ct) =>
        _db.Set<Membership>()
            .CountAsync(m => m.Role == TenantRole.Owner && m.Status == MembershipStatus.Active, ct);

    public Task<User?> FindUserByIdAsync(Guid userId, CancellationToken ct) =>
        _db.Set<User>()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
