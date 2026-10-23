using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Users;
using TallerControl.Domain.Users;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Users;

internal sealed class EfInvitationRepository : IInvitationRepository
{
    private readonly AppDbContext _db;

    public EfInvitationRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Invitation?> FindByTokenHashAsync(byte[] tokenHash, CancellationToken ct) =>
        _db.Set<Invitation>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash, ct);

    public Task<Invitation?> FindPendingByEmailAsync(string email, CancellationToken ct) =>
        _db.Set<Invitation>()
            .Where(i => i.Email == email && i.AcceptedAt == null && i.CancelledAt == null)
            .OrderByDescending(i => i.ExpiresAt)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Invitation>> ListPendingAsync(CancellationToken ct) =>
        await _db.Set<Invitation>().AsNoTracking()
            .Where(i => i.AcceptedAt == null && i.CancelledAt == null)
            .OrderByDescending(i => i.ExpiresAt)
            .ToListAsync(ct);

    public Task<Invitation?> FindByIdAsync(Guid id, CancellationToken ct) =>
        _db.Set<Invitation>().FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<bool> IsActiveMemberAsync(string email, CancellationToken ct) =>
        await _db.Set<Membership>()
            .Join(
                _db.Set<User>().Where(u => u.Email == email),
                m => m.UserId,
                u => u.Id,
                (m, _) => m)
            .AnyAsync(m => m.Status == MembershipStatus.Active, ct);

    public void Add(Invitation invitation) => _db.Set<Invitation>().Add(invitation);

    public void AddUser(User user) => _db.Set<User>().Add(user);

    public void AddMembership(Membership membership) => _db.Set<Membership>().Add(membership);

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
