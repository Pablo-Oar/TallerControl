using TallerControl.Domain.Scheduling;

namespace TallerControl.Application.Scheduling;

/// <summary>Ítem de la lista de turnos.</summary>
public sealed record AppointmentListDto(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    Guid VehicleId,
    string VehiclePlate,
    Guid? MechanicMembershipId,
    string? MechanicName,
    DateTimeOffset ScheduledAt,
    int TotalDurationMinutes,
    AppointmentStatus Status,
    string? Notes);

/// <summary>Detalle completo de un turno.</summary>
public sealed record AppointmentDto(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    Guid VehicleId,
    string VehiclePlate,
    Guid? MechanicMembershipId,
    string? MechanicName,
    DateTimeOffset ScheduledAt,
    int TotalDurationMinutes,
    AppointmentStatus Status,
    string? Notes,
    string? CancellationReason,
    IReadOnlyList<AppointmentServiceDto> Services,
    uint Version);

/// <summary>Servicio dentro de un turno.</summary>
public sealed record AppointmentServiceDto(
    Guid CatalogItemId,
    string CatalogItemName,
    int DurationMinutes,
    int BufferMinutes);

/// <summary>Slot de disponibilidad libre para agendar.</summary>
public sealed record AvailabilitySlotDto(
    Guid? MechanicMembershipId,
    string? MechanicName,
    DateTimeOffset Start,
    DateTimeOffset End);

/// <summary>Regla de disponibilidad semanal de un mecánico.</summary>
public sealed record AvailabilityRuleDto(
    Guid Id,
    Guid MechanicMembershipId,
    string MechanicName,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime);

/// <summary>Excepción de disponibilidad (día bloqueado u horario especial).</summary>
public sealed record AvailabilityExceptionDto(
    Guid Id,
    Guid? MechanicMembershipId,
    string? MechanicName,
    DateOnly Date,
    bool IsBlocked,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    string? Reason);

/// <summary>
/// Respuesta de POST /api/v1/appointments.
/// ConfirmationToken es el raw base64url — solo viaja aquí, nunca persiste.
/// </summary>
public sealed record CreateAppointmentResponse(
    AppointmentDto Appointment,
    string ConfirmationToken);

/// <summary>Cuerpo de POST /api/v1/appointments.</summary>
public sealed record CreateAppointmentRequest(
    Guid CustomerId,
    Guid VehicleId,
    Guid? MechanicMembershipId,
    DateTimeOffset ScheduledAt,
    IReadOnlyList<AppointmentServiceRequest> Services,
    string? Notes = null,
    bool Force = false);

/// <summary>Servicio a incluir en el turno al crearlo.</summary>
public sealed record AppointmentServiceRequest(
    Guid CatalogItemId);

/// <summary>Cuerpo de PUT /api/v1/appointments/{id}.</summary>
public sealed record UpdateAppointmentRequest(
    DateTimeOffset ScheduledAt,
    Guid? MechanicMembershipId,
    string? Notes = null,
    bool Force = false);

/// <summary>
/// Lo que ve el cliente al abrir el enlace de su turno. DTO dedicado: sin datos del taller ni la patente.
/// </summary>
public sealed record PublicAppointmentDto(
    DateTimeOffset ScheduledAt,
    string TimeZone,
    int TotalDurationMinutes,
    AppointmentStatus Status,
    string? VehicleLabel,
    IReadOnlyList<string> Services);

/// <summary>Respuesta de POST /api/v1/appointments/{id}/public-link (el token solo se ve una vez).</summary>
public sealed record AppointmentLinkResponse(string Token);

/// <summary>Cuerpo de DELETE /api/v1/appointments/{id}.</summary>
public sealed record CancelAppointmentRequest(
    string Reason);

/// <summary>Cuerpo de POST /api/v1/appointments/{id}/to-work-order.</summary>
public sealed record ConvertToWorkOrderRequest(
    int OdometerKm);

/// <summary>Cuerpo de POST/PUT /api/v1/availability/rules.</summary>
public sealed record UpsertAvailabilityRuleRequest(
    Guid MechanicMembershipId,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime);

/// <summary>Cuerpo de POST /api/v1/availability/exceptions.</summary>
public sealed record CreateAvailabilityExceptionRequest(
    Guid? MechanicMembershipId,
    DateOnly Date,
    bool IsBlocked,
    TimeOnly? StartTime = null,
    TimeOnly? EndTime = null,
    string? Reason = null);
