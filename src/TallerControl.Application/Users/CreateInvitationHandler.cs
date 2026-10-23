using TallerControl.Application.Common;
using TallerControl.Domain.Users;

namespace TallerControl.Application.Users;

/// <summary>
/// Caso de uso: invitar a un usuario al taller (doc 05 §2, REQ-02, T-USR-01..03).
/// Solo el dueño puede invitar (permiso <c>users.manage</c>, controlado en el endpoint).
/// Si el email ya es miembro activo del taller responde 409 (T-USR-02).
/// El email de invitación sale por outbox (D-04).
/// </summary>
public sealed class CreateInvitationHandler
{
    private const string OutboxType = "email.invitation";

    private readonly IInvitationRepository _invitations;
    private readonly IOutbox _outbox;
    private readonly ITenantContext _tenant;
    private readonly TimeProvider _clock;

    public CreateInvitationHandler(
        IInvitationRepository invitations,
        IOutbox outbox,
        ITenantContext tenant,
        TimeProvider clock)
    {
        _invitations = invitations;
        _outbox = outbox;
        _tenant = tenant;
        _clock = clock;
    }

    public async Task<InvitationDto> HandleAsync(
        CreateInvitationRequest request,
        string frontendBaseUrl,
        CancellationToken ct)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("Se requiere sesión de taller.");

        // Solo Owner puede invitar; validar roles permitidos para invitar.
        if (request.Role == TenantRole.Owner)
        {
            throw new BusinessRuleException("No se puede invitar con el rol Dueño.");
        }

        FinePermissions.Validate(request.Role, request.Permissions);

        var email = request.Email.Trim().ToLowerInvariant();
        var now = _clock.GetUtcNow();

        if (await _invitations.IsActiveMemberAsync(email, ct))
        {
            throw new ConflictException("Esa persona ya forma parte del taller.");
        }

        // Una pendiente vigente se reenvía (no se duplica); una vencida se reemplaza por la nueva.
        var existing = await _invitations.FindPendingByEmailAsync(email, ct);
        if (existing is not null && !existing.IsExpired(now))
        {
            throw new ConflictException(
                "Ya hay una invitación pendiente para ese email: reenviala desde la lista de invitaciones.");
        }

        existing?.Cancel(now);

        var (invitation, token) = Invitation.Create(
            tenantId,
            request.Email,
            request.Role,
            request.Permissions,
            now,
            request.DisplayName);

        _invitations.Add(invitation);

        _outbox.Enqueue(OutboxType, new InvitationEmailPayload(
            To: invitation.Email,
            DisplayName: request.DisplayName,
            Token: token,
            ExpiresAt: invitation.ExpiresAt,
            FrontendBaseUrl: frontendBaseUrl));

        await _invitations.SaveChangesAsync(ct);

        return new InvitationDto(invitation.Id, invitation.Email, invitation.Role, invitation.ExpiresAt);
    }
}

/// <summary>Payload del outbox para el email de invitación.</summary>
internal sealed record InvitationEmailPayload(
    string To,
    string DisplayName,
    string Token,
    DateTimeOffset ExpiresAt,
    string FrontendBaseUrl);
