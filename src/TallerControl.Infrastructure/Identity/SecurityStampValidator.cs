using Microsoft.EntityFrameworkCore;
using TallerControl.Domain.Users;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Identity;

/// <summary>
/// Chequea que el <c>security_stamp</c> del claim siga coincidiendo con el de la base. Si cambió
/// (logout, cambio de contraseña), la cookie se invalida (REQ-02, T-AUTH-05). Se usa desde el
/// evento <c>OnValidatePrincipal</c> de la autenticación por cookie.
/// </summary>
public sealed class SecurityStampValidator
{
    private readonly AppDbContext _db;

    public SecurityStampValidator(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> IsValidAsync(Guid userId, string stamp, CancellationToken ct)
    {
        var current = await _db.Set<User>()
            .Where(u => u.Id == userId && u.Status == UserStatus.Active)
            .Select(u => u.SecurityStamp)
            .FirstOrDefaultAsync(ct);

        return current is not null && current == stamp;
    }
}
