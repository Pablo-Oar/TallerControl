using TallerControl.Application.Common;
using TallerControl.Domain.Users;


namespace TallerControl.Application.Users;

/// <summary>
/// Caso de uso: cambiar rol, permisos extra o estado de una membresía (doc 05 §2, REQ-03).
/// Solo Owner puede invocar este endpoint.
/// - T-USR-05: quitar el rol al único dueño → 422.
/// - T-USR-06: cambiar rol o desactivar rota el security_stamp → la sesión queda inválida.
/// - T-USR-07: desactivar conserva los registros históricos (el usuario sigue en la DB).
/// </summary>
public sealed class UpdateMembershipHandler
{
    private readonly IMembershipRepository _repo;

    public UpdateMembershipHandler(IMembershipRepository repo)
    {
        _repo = repo;
    }

    public async Task<MemberDto> HandleAsync(
        Guid membershipId,
        UpdateMembershipRequest request,
        uint expectedVersion,
        CancellationToken ct)
    {
        var membership = await _repo.FindByIdAsync(membershipId, ct)
            ?? throw new NotFoundException();

        if (membership.Version != expectedVersion)
        {
            throw new ConcurrencyException();
        }

        // Los permisos extra se validan contra el rol que va a quedar.
        FinePermissions.Validate(request.Role ?? membership.Role, request.Permissions);

        bool rotateStamp = false;

        if (request.Role.HasValue && request.Role.Value != membership.Role)
        {
            // T-USR-05: no se puede quitar el rol de dueño si es el único activo.
            if (membership.Role == TenantRole.Owner)
            {
                var ownerCount = await _repo.CountActiveOwnersAsync(ct);
                if (ownerCount <= 1)
                {
                    throw new BusinessRuleException(
                        "No se puede cambiar el rol del único dueño del taller.",
                        new Dictionary<string, string[]> { ["role"] = ["Es el único dueño activo."] });
                }
            }

            membership.ChangeRole(request.Role.Value, request.Permissions);
            rotateStamp = true;
        }
        else if (request.Permissions is not null)
        {
            membership.UpdatePermissions(request.Permissions);
        }

        if (request.Status.HasValue && request.Status.Value != membership.Status)
        {
            if (request.Status.Value == MembershipStatus.Disabled)
            {
                // T-USR-05: no se puede desactivar al único dueño.
                if (membership.Role == TenantRole.Owner)
                {
                    var ownerCount = await _repo.CountActiveOwnersAsync(ct);
                    if (ownerCount <= 1)
                    {
                        throw new BusinessRuleException(
                            "No se puede desactivar al único dueño del taller.",
                            new Dictionary<string, string[]> { ["status"] = ["Es el único dueño activo."] });
                    }
                }

                membership.Disable();
            }
            else
            {
                membership.Enable();
            }

            rotateStamp = true;
        }

        // T-USR-06: rotar el security_stamp invalida la sesión activa en menos de 5 min.
        if (rotateStamp)
        {
            var user = await _repo.FindUserByIdAsync(membership.UserId, ct)
                ?? throw new NotFoundException("El usuario de la membresía no existe.");
            user.RotateSecurityStamp();
        }

        await _repo.SaveChangesAsync(ct);

        var user2 = await _repo.FindUserByIdAsync(membership.UserId, ct)
            ?? throw new NotFoundException();

        return new MemberDto(
            membership.Id,
            membership.UserId,
            user2.DisplayName,
            user2.Email,
            membership.Role,
            membership.Permissions,
            membership.Status,
            membership.Version);
    }
}
