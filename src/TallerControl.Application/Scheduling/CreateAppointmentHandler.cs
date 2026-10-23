using TallerControl.Application.Common;
using TallerControl.Domain.Scheduling;

namespace TallerControl.Application.Scheduling;

/// <summary>
/// Crea un turno. Sin mecánico, elige al que tenga menos carga y esté libre (T-APT-01). Con mecánico, el servidor
/// valida su horario y que no tenga otro turno encima (A-05); el Dueño puede forzarlo. Si el cliente tiene email,
/// le sale el enlace para confirmar (A-03, N-01).
/// </summary>
public sealed class CreateAppointmentHandler(
    ISchedulingRepository repo,
    ITenantContext tenant,
    IOutbox outbox)
{
    public async Task<CreateAppointmentResponse> HandleAsync(
        CreateAppointmentRequest request, bool canForce, CancellationToken ct)
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

        if (request.Force && !canForce)
            throw new BusinessRuleException("Solo el Dueño puede agendar un turno fuera de horario o encimado.");

        // Servicios del catálogo con su duración (la copia queda en el turno).
        var services = new List<(Guid CatalogItemId, int Duration, int Buffer)>();
        foreach (var svcReq in request.Services)
        {
            var info = await repo.GetCatalogItemInfoAsync(svcReq.CatalogItemId, ct)
                ?? throw new NotFoundException($"El servicio {svcReq.CatalogItemId} no existe.");

            if (!info.DurationMinutes.HasValue || info.DurationMinutes.Value <= 0)
                throw new BusinessRuleException(
                    $"El servicio '{info.Name}' no tiene una duración configurada.");

            services.Add((svcReq.CatalogItemId, info.DurationMinutes.Value, info.BufferMinutes ?? 0));
        }

        var minutes = services.Sum(s => s.Duration + s.Buffer);

        // Mecánico: el pedido (validado) o el que corresponda según carga y disponibilidad.
        var mechanicId = request.MechanicMembershipId;
        if (mechanicId.HasValue)
        {
            if (!request.Force)
                await EnsureFreeAsync(mechanicId.Value, request.ScheduledAt, minutes, null, ct);
        }
        else
        {
            var (picked, conflict) = await repo.PickMechanicAsync(request.ScheduledAt, minutes, ct);
            if (conflict is not null && !request.Force)
                throw new BusinessRuleException(await WithSuggestionAsync(conflict.Message, null, request.ScheduledAt, minutes, ct));
            mechanicId = picked;
        }

        var (token, hash) = AppointmentToken.New();

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
                hash);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        foreach (var (catalogItemId, duration, buffer) in services)
            appointment.AddService(new AppointmentService(appointment.Id, catalogItemId, duration, buffer));

        await repo.AddAsync(appointment, ct);

        await EnqueueLinkEmailAsync(appointment, token, ct);

        await repo.SaveChangesAsync(ct);

        var dto = await repo.GetFullAsync(appointment.Id, ct)
            ?? throw new InvalidOperationException("El turno recién creado no se pudo leer.");

        return new CreateAppointmentResponse(dto, token);
    }

    private async Task EnsureFreeAsync(
        Guid mechanicId, DateTimeOffset start, int minutes, Guid? excludeId, CancellationToken ct)
    {
        var conflict = await repo.CheckSlotAsync(mechanicId, start, minutes, excludeId, ct);
        if (conflict is not null)
            throw new BusinessRuleException(await WithSuggestionAsync(conflict.Message, mechanicId, start, minutes, ct));
    }

    private async Task<string> WithSuggestionAsync(
        string message, Guid? mechanicId, DateTimeOffset from, int minutes, CancellationToken ct)
    {
        var next = await repo.FindNextFreeSlotAsync(mechanicId, from, minutes, ct);
        if (next is null) return message;

        var zone = TimeZoneId(await repo.GetTenantTimeZoneIdAsync(ct));
        var local = TimeZoneInfo.ConvertTime(next.Start, zone);
        return $"{message} Próximo hueco libre: {local:dddd dd/MM HH:mm}.";
    }

    private async Task EnqueueLinkEmailAsync(Appointment appointment, string token, CancellationToken ct)
    {
        var contact = await repo.GetCustomerContactAsync(appointment.CustomerId, ct);
        if (contact is null || contact.DoNotContact || string.IsNullOrWhiteSpace(contact.Email)) return;

        outbox.Enqueue("email.appointment", new
        {
            to = contact.Email,
            customerName = contact.Name,
            token,
            scheduledAt = appointment.ScheduledAt,
            timeZone = await repo.GetTenantTimeZoneIdAsync(ct),
        });
    }

    private static TimeZoneInfo TimeZoneId(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
