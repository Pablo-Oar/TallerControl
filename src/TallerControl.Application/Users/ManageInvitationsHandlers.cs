using TallerControl.Application.Common;

namespace TallerControl.Application.Users;

/// <summary>U-01: invitaciones pendientes del taller.</summary>
public sealed class ListInvitationsHandler(IInvitationRepository invitations, TimeProvider clock)
{
    public async Task<IReadOnlyList<PendingInvitationDto>> HandleAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return (await invitations.ListPendingAsync(ct))
            .Select(i => new PendingInvitationDto(i.Id, i.Email, i.DisplayName, i.Role, i.ExpiresAt, i.IsExpired(now)))
            .ToList();
    }
}

/// <summary>Reenvía una invitación: token nuevo (el enlace anterior deja de servir), nuevo vencimiento y nuevo email.</summary>
public sealed class ResendInvitationHandler(IInvitationRepository invitations, IOutbox outbox, TimeProvider clock)
{
    public async Task<PendingInvitationDto> HandleAsync(Guid id, string frontendBaseUrl, CancellationToken ct)
    {
        var invitation = await invitations.FindByIdAsync(id, ct) ?? throw new NotFoundException();

        string token;
        try
        {
            token = invitation.Reissue(clock.GetUtcNow());
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        outbox.Enqueue("email.invitation", new InvitationEmailPayload(
            To: invitation.Email,
            DisplayName: invitation.DisplayName ?? invitation.Email.Split('@')[0],
            Token: token,
            ExpiresAt: invitation.ExpiresAt,
            FrontendBaseUrl: frontendBaseUrl));

        await invitations.SaveChangesAsync(ct);
        return new PendingInvitationDto(
            invitation.Id, invitation.Email, invitation.DisplayName, invitation.Role, invitation.ExpiresAt, false);
    }
}

/// <summary>Cancela una invitación pendiente: el enlace deja de servir.</summary>
public sealed class CancelInvitationHandler(IInvitationRepository invitations, TimeProvider clock)
{
    public async Task HandleAsync(Guid id, CancellationToken ct)
    {
        var invitation = await invitations.FindByIdAsync(id, ct) ?? throw new NotFoundException();

        try
        {
            invitation.Cancel(clock.GetUtcNow());
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        await invitations.SaveChangesAsync(ct);
    }
}
