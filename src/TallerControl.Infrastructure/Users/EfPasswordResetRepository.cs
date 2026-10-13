using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Users;
using TallerControl.Domain.Users;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Users;

internal sealed class EfPasswordResetRepository : IPasswordResetRepository
{
    private readonly AppDbContext _db;

    public EfPasswordResetRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<PasswordReset?> FindByTokenHashAsync(byte[] tokenHash, CancellationToken ct) =>
        _db.Set<PasswordReset>()
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash, ct);

    public void Add(PasswordReset reset) => _db.Set<PasswordReset>().Add(reset);

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
