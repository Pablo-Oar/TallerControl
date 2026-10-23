using TallerControl.Application.Common;
using TallerControl.Domain.Users;

namespace TallerControl.Application.Auth;

/// <summary>
/// Resuelve el contexto de la sesión actual: usuario, taller activo, rol y permisos efectivos
/// (doc 05 §2). Los permisos se recalculan en cada request contra la base: cambios de rol o de
/// permisos finos se aplican sin re-login.
/// </summary>
public sealed class MeHandler
{
    private readonly IAuthRepository _repository;

    public MeHandler(IAuthRepository repository)
    {
        _repository = repository;
    }

    public async Task<MeResponse> HandleAsync(Guid userId, CancellationToken ct)
    {
        var user = await _repository.FindUserByIdAsync(userId, ct)
            ?? throw new NotFoundException();

        var membership = await _repository.FindActiveMembershipAsync(user.Id, ct)
            ?? throw new NotFoundException();

        var permissions = membership.Membership.EffectivePermissions()
            .OrderBy(static p => p, StringComparer.Ordinal)
            .ToList();

        var home = membership.Membership.Role == TenantRole.Mechanic ? "/m" : "/app";

        return new MeResponse(
            User: new MeUserDto(user.Id, user.Email, user.DisplayName),
            Tenant: new MeTenantDto(membership.Tenant.Id, membership.Tenant.Name, membership.DefaultVatRate),
            Role: membership.Membership.Role,
            Permissions: permissions,
            TimeZone: membership.Tenant.TimeZone,
            Home: home,
            PlanCode: membership.Tenant.PlanCode);
    }
}
