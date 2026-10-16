using System.Security.Cryptography;
using TallerControl.Application.Common;
using TallerControl.Domain.Scheduling;

namespace TallerControl.Application.Scheduling;

/// <summary>
/// Crea un turno. Si MechanicMembershipId es null, asigna el mecánico con menos carga ese día (T-APT-01).
/// </summary>
public sealed class CreateAppointmentHandler(
    ISchedulingRepository repo,
    ITenantContext tenant)
{
    public async Task<CreateAppointmentResponse> HandleAsync(CreateAppointmentRequest request, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        if (!await repo.CustomerExistsAsync(request.CustomerId, ct))
            throw new NotFoundException("El cliente no existe.");

        if (!await repo.VehicleExistsAsync(request.VehicleId, ct))
            throw new NotFoundException("El vehículo no existe.");

        if (request.MechanicMembershipId.HasValue
            && !await repo.MembershipExistsAsync(request.MechanicMembershipId.Value, ct))
            throw new NotFoundException("El mecánico no existe.");

        if (request.Services == null || request.Services.Count == 0)
            throw new BusinessRuleException("El turno debe tener al menos un servicio.");

        // Generar token de confirmación: raw base64url solo en la respuesta, hash en BD.
        var rawToken = RandomNumberGenerator.GetBytes(32);
        var tokenHash = SHA256.HashData(rawToken);
        var tokenBase64Url = Convert.ToBase64String(rawToken)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        // Asignar mecánico automáticamente si no se especificó (T-APT-01).
        var mechanicId = request.MechanicMembershipId;
        if (!mechanicId.HasValue)
        {
            var scheduledDate = DateOnly.FromDateTime(request.ScheduledAt.UtcDateTime);
            mechanicId = await repo.FindMechanicWithLeastWorkloadAsync(scheduledDate, ct);
        }

        Appointment appointment;
        try
        {
            appointment = new Appointment(
                tenantId,
                request.CustomerId,
                request.VehicleId,
                mechanicId,
                request.ScheduledAt,
                request.Notes,
                tokenHash);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        // Agregar servicios con copia de duración/intervalo del catálogo.
        foreach (var svcReq in request.Services)
        {
            var info = await repo.GetCatalogItemInfoAsync(svcReq.CatalogItemId, ct)
                ?? throw new NotFoundException($"El servicio {svcReq.CatalogItemId} no existe.");

            if (!info.DurationMinutes.HasValue || info.DurationMinutes.Value <= 0)
                throw new BusinessRuleException(
                    $"El servicio '{info.Name}' no tiene una duración configurada.");

            var svc = new AppointmentService(
                appointment.Id,
                svcReq.CatalogItemId,
                info.DurationMinutes.Value,
                info.BufferMinutes ?? 0);

            appointment.AddService(svc);
        }

        await repo.AddAsync(appointment, ct);
        await repo.SaveChangesAsync(ct);

        var dto = await repo.GetFullAsync(appointment.Id, ct)
            ?? throw new InvalidOperationException("El turno recién creado no se pudo leer.");

        return new CreateAppointmentResponse(dto, tokenBase64Url);
    }
}
