using TallerControl.Domain.Common;

namespace TallerControl.Domain.Scheduling;

/// <summary>
/// Servicio del catálogo incluido en un turno. Copia DurationMinutes y BufferMinutes
/// en el momento de agregar para que cambios posteriores en el catálogo no alteren el bloque reservado.
/// </summary>
public sealed class AppointmentService : Entity
{
    private AppointmentService()
    {
    }

    public AppointmentService(
        Guid appointmentId,
        Guid catalogItemId,
        int durationMinutes,
        int bufferMinutes)
    {
        if (appointmentId == Guid.Empty)
            throw new ArgumentException("El servicio necesita un turno.", nameof(appointmentId));

        if (catalogItemId == Guid.Empty)
            throw new ArgumentException("El servicio necesita un ítem de catálogo.", nameof(catalogItemId));

        if (durationMinutes <= 0)
            throw new ArgumentOutOfRangeException(nameof(durationMinutes), "La duración debe ser mayor a cero.");

        if (bufferMinutes < 0)
            throw new ArgumentOutOfRangeException(nameof(bufferMinutes), "El intervalo no puede ser negativo.");

        AppointmentId = appointmentId;
        CatalogItemId = catalogItemId;
        DurationMinutes = durationMinutes;
        BufferMinutes = bufferMinutes;
    }

    public Guid AppointmentId { get; private set; }
    public Guid CatalogItemId { get; private set; }

    /// <summary>Copia de CatalogItem.DurationMinutes al momento de reservar el turno.</summary>
    public int DurationMinutes { get; private set; }

    /// <summary>Copia de CatalogItem.BufferMinutes al momento de reservar el turno.</summary>
    public int BufferMinutes { get; private set; }

    /// <summary>Bloque total que ocupa este servicio = DurationMinutes + BufferMinutes (T-APT-04).</summary>
    public int TotalBlockMinutes => DurationMinutes + BufferMinutes;
}
