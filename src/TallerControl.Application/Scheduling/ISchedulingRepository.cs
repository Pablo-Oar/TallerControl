using TallerControl.Application.Common;
using TallerControl.Domain.Scheduling;

namespace TallerControl.Application.Scheduling;

/// <summary>
/// Contrato de persistencia para el módulo de agenda.
/// Incluye turnos, reglas de disponibilidad y excepciones.
/// </summary>
public interface ISchedulingRepository
{
    // ── Appointments ──────────────────────────────────────────────────────────

    /// <summary>Los días son de la zona horaria del taller. <c>from</c>/<c>to</c> inclusivos.</summary>
    Task<Page<AppointmentListDto>> SearchAsync(
        DateOnly? from, DateOnly? to, Guid? mechanicMembershipId, AppointmentStatus? status,
        int page, int pageSize, CancellationToken ct);

    Task<AppointmentDto?> GetFullAsync(Guid id, CancellationToken ct);

    Task<Appointment?> FindByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Busca por hash del token de confirmación (acceso público anónimo).</summary>
    Task<Appointment?> FindByConfirmationTokenHashAsync(byte[] tokenHash, CancellationToken ct);

    Task AddAsync(Appointment appointment, CancellationToken ct);

    // ── Availability ──────────────────────────────────────────────────────────

    Task<IReadOnlyList<AvailabilityRuleDto>> GetRulesAsync(Guid? mechanicMembershipId, CancellationToken ct);

    Task<AvailabilityRule?> FindRuleByIdAsync(Guid id, CancellationToken ct);

    Task AddRuleAsync(AvailabilityRule rule, CancellationToken ct);

    Task DeleteRuleAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<AvailabilityExceptionDto>> GetExceptionsAsync(DateOnly date, CancellationToken ct);

    /// <summary>Excepciones entre dos fechas (ambas inclusivas), para la pantalla de feriados y vacaciones.</summary>
    Task<IReadOnlyList<AvailabilityExceptionDto>> GetExceptionsRangeAsync(DateOnly from, DateOnly to, CancellationToken ct);

    Task<AvailabilityException?> FindExceptionByIdAsync(Guid id, CancellationToken ct);

    Task AddExceptionAsync(AvailabilityException exception, CancellationToken ct);

    void RemoveException(AvailabilityException exception);

    /// <summary>
    /// Calcula slots libres para la fecha dada cruzando reglas, excepciones y turnos existentes.
    /// </summary>
    Task<IReadOnlyList<AvailabilitySlotDto>> GetAvailabilitySlotsAsync(
        DateOnly date, int requiredMinutes, CancellationToken ct);

    /// <summary>
    /// Devuelve el mecánico con menos turnos activos el día de scheduledAt (T-APT-01).
    /// Null si no hay mecánicos con reglas de disponibilidad para ese día.
    /// </summary>
    Task<Guid?> FindMechanicWithLeastWorkloadAsync(DateOnly date, CancellationToken ct);

    /// <summary>
    /// A-05: ¿puede el mecánico atender un turno que empieza en <paramref name="start"/> y dura <paramref name="minutes"/>?
    /// Revisa su horario semanal (si lo tiene cargado), los bloqueos y los turnos que ya tiene. Null = libre.
    /// </summary>
    Task<SlotConflict?> CheckSlotAsync(
        Guid mechanicId, DateTimeOffset start, int minutes, Guid? excludeAppointmentId, CancellationToken ct);

    /// <summary>Próximo hueco libre desde <paramref name="from"/> (hasta 14 días); de un mecánico o de cualquiera.</summary>
    Task<AvailabilitySlotDto?> FindNextFreeSlotAsync(
        Guid? mechanicId, DateTimeOffset from, int minutes, CancellationToken ct);

    /// <summary>
    /// Elige para un turno sin mecánico al que tenga menos carga ese día y esté libre a esa hora.
    /// Sin mecánicos con horario cargado ese día devuelve (null, null): el turno queda sin asignar.
    /// Con mecánicos pero ninguno libre devuelve el conflicto.
    /// </summary>
    Task<(Guid? MechanicId, SlotConflict? Conflict)> PickMechanicAsync(
        DateTimeOffset start, int minutes, CancellationToken ct);

    /// <summary>Contacto del cliente para enviarle el enlace del turno.</summary>
    Task<CustomerContact?> GetCustomerContactAsync(Guid customerId, CancellationToken ct);

    Task<string> GetTenantTimeZoneIdAsync(CancellationToken ct);

    /// <summary>Resumen público por hash del token (acceso anónimo, sin filtros de taller).</summary>
    Task<PublicAppointmentDto?> GetPublicSummaryAsync(byte[] tokenHash, CancellationToken ct);

    // ── Helpers ───────────────────────────────────────────────────────────────

    Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken ct);
    Task<bool> VehicleExistsAsync(Guid vehicleId, CancellationToken ct);
    Task<bool> MembershipExistsAsync(Guid membershipId, CancellationToken ct);

    /// <summary>Devuelve el nombre + DurationMinutes + BufferMinutes del ítem del catálogo.</summary>
    Task<CatalogItemInfo?> GetCatalogItemInfoAsync(Guid catalogItemId, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}

/// <summary>Motivo por el que un mecánico no puede tomar un turno en ese horario.</summary>
public sealed record SlotConflict(string Message);

public sealed record CustomerContact(string Name, string? Email, bool DoNotContact);

/// <summary>Proyección mínima del catálogo necesaria al crear un turno.</summary>
public sealed record CatalogItemInfo(string Name, int? DurationMinutes, int? BufferMinutes);
