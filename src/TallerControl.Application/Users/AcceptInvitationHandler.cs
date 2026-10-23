using TallerControl.Application.Auth;
using TallerControl.Application.Common;
using TallerControl.Domain.Users;

namespace TallerControl.Application.Users;

/// <summary>
/// Caso de uso: aceptar una invitación (doc 05 §2, REQ-02, T-USR-04).
/// Surface pública: DTO dedicado, token solo como hash, respuesta uniforme (doc 07 §2).
/// Si el token venció o ya fue aceptado responde 422.
/// </summary>
public sealed class AcceptInvitationHandler
{
    private readonly IInvitationRepository _invitations;
    private readonly IAuthRepository _auth;
    private readonly IPasswordHasher _hasher;
    private readonly TimeProvider _clock;

    public AcceptInvitationHandler(
        IInvitationRepository invitations,
        IAuthRepository auth,
        IPasswordHasher hasher,
        TimeProvider clock)
    {
        _invitations = invitations;
        _auth = auth;
        _hasher = hasher;
        _clock = clock;
    }

    /// <summary>
    /// Devuelve los datos de sesión para que el endpoint pueda emitir la cookie.
    /// </summary>
    public async Task<AcceptResult> HandleAsync(
        string token,
        AcceptInvitationRequest request,
        CancellationToken ct)
    {
        if (request.Password.Length < User.MinPasswordLength)
        {
            throw new BusinessRuleException(
                $"La contraseña debe tener al menos {User.MinPasswordLength} caracteres.",
                new Dictionary<string, string[]>
                {
                    ["password"] = [$"Mínimo {User.MinPasswordLength} caracteres."],
                });
        }

        var tokenHash = Invitation.HashToken(token);
        var invitation = await _invitations.FindByTokenHashAsync(tokenHash, ct);

        var now = _clock.GetUtcNow();

        if (invitation is null || invitation.IsExpired(now) || invitation.IsAccepted || invitation.CancelledAt is not null)
        {
            throw new BusinessRuleException("El enlace de invitación no es válido o ya expiró.");
        }

        // Crear o reusar usuario (puede ya existir de otro taller, doc 04 §3.1).
        var user = await _auth.FindUserByEmailAsync(invitation.Email, ct);
        bool isNewUser = user is null;
        if (isNewUser)
        {
            // El nombre con el que lo invitaron; sin él, la parte local del email hasta que lo edite en su perfil.
            user = new User(invitation.Email, invitation.DisplayName ?? invitation.Email.Split('@')[0]);
        }

        user!.Activate(_hasher.Hash(request.Password));

        invitation.Accept(now);

        var membership = new Membership(
            invitation.TenantId,
            user.Id,
            invitation.Role,
            invitation.Permissions.Count > 0 ? invitation.Permissions : null);

        if (isNewUser)
        {
            _invitations.AddUser(user);
        }

        _invitations.AddMembership(membership);

        await _invitations.SaveChangesAsync(ct);

        return new AcceptResult(user.Id, user.SecurityStamp, invitation.TenantId, invitation.Role);
    }
}

/// <summary>Resultado del caso de uso: datos para emitir la cookie de sesión.</summary>
public sealed record AcceptResult(
    Guid UserId,
    string SecurityStamp,
    Guid TenantId,
    TenantRole Role);
