using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Registration;
using TallerControl.Domain.Users;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Users;

internal sealed class EfRegistrationRepository : IRegistrationRepository
{
    private readonly AppDbContext _db;

    public EfRegistrationRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<bool> IsEmailTakenAsync(string email, CancellationToken ct) =>
        _db.Set<User>().AnyAsync(u => u.Email == email, ct);

    public async Task CreateTenantWithOwnerAsync(RegistrationAggregate a, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        _db.Add(a.Tenant);
        _db.Add(a.Settings);
        _db.Add(a.MainBranch);
        _db.Add(a.Owner);
        _db.Add(a.OwnerMembership);
        _db.Add(a.VerificationToken);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public Task<EmailVerificationToken?> FindVerificationTokenByHashAsync(byte[] tokenHash, CancellationToken ct) =>
        _db.Set<EmailVerificationToken>()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public Task<User?> FindUserByIdAsync(Guid userId, CancellationToken ct) =>
        _db.Set<User>().FirstOrDefaultAsync(u => u.Id == userId, ct);

    public Task<User?> FindUserByEmailAsync(string email, CancellationToken ct) =>
        _db.Set<User>().FirstOrDefaultAsync(u => u.Email == email, ct);

    public async Task<DateTimeOffset?> LastVerificationTokenAtAsync(Guid userId, CancellationToken ct) =>
        await _db.Set<EmailVerificationToken>().AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.ExpiresAt)
            .Select(t => (DateTimeOffset?)t.ExpiresAt)
            .FirstOrDefaultAsync(ct);

    public void AddVerificationToken(EmailVerificationToken token) => _db.Set<EmailVerificationToken>().Add(token);

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
