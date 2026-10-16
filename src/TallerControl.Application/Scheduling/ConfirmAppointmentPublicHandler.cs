using System.Security.Cryptography;
using TallerControl.Application.Common;
using TallerControl.Domain.Scheduling;

namespace TallerControl.Application.Scheduling;

/// <summary>
/// Confirma un turno a través del link enviado al cliente (T-APT-03).
/// Respuesta uniforme: 409 si ya confirmado, 404 si el token no existe o fue revocado.
/// </summary>
public sealed class ConfirmAppointmentPublicHandler(ISchedulingRepository repo)
{
    public async Task HandleAsync(string tokenBase64Url, CancellationToken ct)
    {
        byte[] raw;
        try
        {
            var padded = tokenBase64Url
                .Replace('-', '+').Replace('_', '/')
                + new string('=', (4 - tokenBase64Url.Length % 4) % 4);
            raw = Convert.FromBase64String(padded);
        }
        catch
        {
            throw new NotFoundException();
        }

        var hash = SHA256.HashData(raw);

        var appointment = await repo.FindByConfirmationTokenHashAsync(hash, ct)
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
