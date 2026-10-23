using TallerControl.Application.Common;
using TallerControl.Domain.Scheduling;

namespace TallerControl.Application.Scheduling;

/// <summary>Lo que ve el cliente al abrir el enlace de su turno. 404 si el token no existe, venció o se anuló el turno.</summary>
public sealed class GetPublicAppointmentHandler(ISchedulingRepository repo)
{
    public async Task<PublicAppointmentDto> HandleAsync(string tokenBase64Url, CancellationToken ct) =>
        await repo.GetPublicSummaryAsync(AppointmentToken.Hash(tokenBase64Url), ct)
        ?? throw new NotFoundException();
}

/// <summary>
/// Confirma un turno a través del enlace enviado al cliente (T-APT-03).
/// 409 si ya estaba confirmado, 404 si el token no existe o el turno ya no está abierto.
/// </summary>
public sealed class ConfirmAppointmentPublicHandler(ISchedulingRepository repo)
{
    public async Task HandleAsync(string tokenBase64Url, CancellationToken ct)
    {
        var appointment = await repo.FindByConfirmationTokenHashAsync(AppointmentToken.Hash(tokenBase64Url), ct)
            ?? throw new NotFoundException();

        if (appointment.Status == AppointmentStatus.Confirmed)
            throw new ConflictException("El turno ya fue confirmado.");

        if (appointment.Status != AppointmentStatus.Pending)
            throw new NotFoundException();

        try
        {
            appointment.Confirm();
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        await repo.SaveChangesAsync(ct);
    }
}

/// <summary>«No puedo ir»: el cliente cancela su turno desde el enlace. El taller lo ve cancelado con ese motivo.</summary>
public sealed class DeclineAppointmentPublicHandler(ISchedulingRepository repo)
{
    public async Task HandleAsync(string tokenBase64Url, CancellationToken ct)
    {
        var appointment = await repo.FindByConfirmationTokenHashAsync(AppointmentToken.Hash(tokenBase64Url), ct)
            ?? throw new NotFoundException();

        if (appointment.Status is not (AppointmentStatus.Pending or AppointmentStatus.Confirmed))
            throw new NotFoundException();

        appointment.Cancel("El cliente avisó que no puede asistir.");
        await repo.SaveChangesAsync(ct);
    }
}

/// <summary>Emite un enlace nuevo para un turno abierto (reemplaza al anterior). El token solo se ve en esta respuesta.</summary>
public sealed class GetAppointmentLinkHandler(ISchedulingRepository repo)
{
    public async Task<AppointmentLinkResponse> HandleAsync(Guid appointmentId, CancellationToken ct)
    {
        var appointment = await repo.FindByIdAsync(appointmentId, ct) ?? throw new NotFoundException();

        var (token, hash) = AppointmentToken.New();
        try
        {
            appointment.ResetConfirmationToken(hash);
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        await repo.SaveChangesAsync(ct);
        return new AppointmentLinkResponse(token);
    }
}
