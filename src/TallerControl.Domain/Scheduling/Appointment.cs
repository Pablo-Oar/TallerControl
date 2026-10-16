using TallerControl.Domain.Common;

namespace TallerControl.Domain.Scheduling;

/// <summary>
/// Turno agendado para un vehículo. Máquina de estados: Pending → Confirmed / Cancelled;
/// Pending/Confirmed → Completed (al convertir a orden de trabajo).
/// El token de confirmación se almacena solo como hash SHA-256; el raw solo viaja en la respuesta de creación.
/// </summary>
public sealed class Appointment : Entity, ITenantOwned, IVersioned, IAuditable
{
    private readonly List<AppointmentService> _services = [];

    private Appointment()
    {
    }

    public Appointment(
        Guid tenantId,
        Guid customerId,
        Guid vehicleId,
        Guid? mechanicMembershipId,
        DateTimeOffset scheduledAt,
        string? notes,
        byte[] confirmationTokenHash)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("El turno necesita un taller.", nameof(tenantId));

        if (customerId == Guid.Empty)
            throw new ArgumentException("El turno necesita un cliente.", nameof(customerId));

        if (vehicleId == Guid.Empty)
            throw new ArgumentException("El turno necesita un vehículo.", nameof(vehicleId));

        if (scheduledAt == default)
            throw new ArgumentException("El turno necesita una fecha y hora.", nameof(scheduledAt));

        if (confirmationTokenHash is null || confirmationTokenHash.Length == 0)
            throw new ArgumentException("El turno necesita un hash de token.", nameof(confirmationTokenHash));

        TenantId = tenantId;
        CustomerId = customerId;
        VehicleId = vehicleId;
        MechanicMembershipId = mechanicMembershipId;
        ScheduledAt = scheduledAt;
        Notes = notes?.Trim();
        Status = AppointmentStatus.Pending;
        ConfirmationTokenHash = confirmationTokenHash;
    }

    public Guid TenantId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid VehicleId { get; private set; }

    /// <summary>Null = "cualquier mecánico"; se asigna al crear si hay mecánicos disponibles.</summary>
    public Guid? MechanicMembershipId { get; private set; }

    public DateTimeOffset ScheduledAt { get; private set; }

    /// <summary>Duración total del turno en minutos (suma de bloques de servicios).</summary>
    public int TotalDurationMinutes { get; private set; }

    public AppointmentStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public string? CancellationReason { get; private set; }

    /// <summary>Hash SHA-256 del token de confirmación. Null cuando ya fue confirmado o cancelado.</summary>
    public byte[]? ConfirmationTokenHash { get; private set; }

    public uint Version { get; private set; }

    // IAuditable
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public IReadOnlyList<AppointmentService> Services => _services.AsReadOnly();

    public void AddService(AppointmentService service)
    {
        if (Status != AppointmentStatus.Pending)
            throw new InvalidOperationException("Solo se pueden agregar servicios a un turno pendiente.");

        _services.Add(service);
        TotalDurationMinutes = _services.Sum(s => s.TotalBlockMinutes);
    }

    /// <summary>
    /// Asigna el mecánico con menor carga del día (T-APT-01).
    /// Llamado por el handler cuando MechanicMembershipId es null.
    /// </summary>
    public void AssignMechanic(Guid mechanicMembershipId)
    {
        if (mechanicMembershipId == Guid.Empty)
            throw new ArgumentException("El id del mecánico no puede ser vacío.", nameof(mechanicMembershipId));

        MechanicMembershipId = mechanicMembershipId;
    }

    public void Update(DateTimeOffset scheduledAt, Guid? mechanicMembershipId, string? notes)
    {
        if (Status is AppointmentStatus.Completed or AppointmentStatus.Cancelled)
            throw new InvalidOperationException($"No se puede modificar un turno en estado {Status}.");

        if (scheduledAt == default)
            throw new ArgumentException("El turno necesita una fecha y hora.", nameof(scheduledAt));

        ScheduledAt = scheduledAt;
        MechanicMembershipId = mechanicMembershipId;
        Notes = notes?.Trim();
    }

    /// <summary>Confirma el turno a través del link de cliente (T-APT-03).</summary>
    public void Confirm()
    {
        if (Status == AppointmentStatus.Confirmed)
            throw new InvalidOperationException("El turno ya estaba confirmado.");

        if (Status != AppointmentStatus.Pending)
            throw new InvalidOperationException($"No se puede confirmar un turno en estado {Status}.");

        Status = AppointmentStatus.Confirmed;
        ConfirmationTokenHash = null;
    }

    /// <summary>
    /// Marca el turno como completado al convertirlo en OT (T-APT-02).
    /// Solo permitido desde Pending o Confirmed.
    /// </summary>
    public void Complete()
    {
        if (Status is AppointmentStatus.Completed or AppointmentStatus.Cancelled)
            throw new InvalidOperationException($"No se puede completar un turno en estado {Status}.");

        Status = AppointmentStatus.Completed;
        ConfirmationTokenHash = null;
    }

    public void Cancel(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("El motivo de cancelación es obligatorio.", nameof(reason));

        if (Status is AppointmentStatus.Completed or AppointmentStatus.Cancelled)
            throw new InvalidOperationException($"No se puede cancelar un turno en estado {Status}.");

        Status = AppointmentStatus.Cancelled;
        CancellationReason = reason.Trim();
        ConfirmationTokenHash = null;
    }
}
