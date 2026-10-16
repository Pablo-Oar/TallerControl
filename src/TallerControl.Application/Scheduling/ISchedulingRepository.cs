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

    Task<Page<AppointmentListDto>> SearchAsync(
        DateOnly? date, Guid? mechanicMembershipId, AppointmentStatus? status,
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken ct);
    Task<bool> VehicleExistsAsync(Guid vehicleId, CancellationToken ct);
    Task<bool> MembershipExistsAsync(Guid membershipId, CancellationToken ct);

    /// <summary>Devuelve el nombre + DurationMinutes + BufferMinutes del ítem del catálogo.</summary>
    Task<CatalogItemInfo?> GetCatalogItemInfoAsync(Guid catalogItemId, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}

/// <summary>Proyección mínima del catálogo necesaria al crear un turno.</summary>
public sealed record CatalogItemInfo(string Name, int? DurationMinutes, int? BufferMinutes);
