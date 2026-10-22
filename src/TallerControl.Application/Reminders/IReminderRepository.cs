using TallerControl.Application.Common;
using TallerControl.Domain.Notifications;
using TallerControl.Domain.Reminders;

namespace TallerControl.Application.Reminders;

public interface IReminderRepository
{
    /// <param name="status">pending (por defecto), completed o all.</param>
    Task<Page<ReminderListDto>> SearchAsync(Guid? vehicleId, string status, int page, int pageSize, CancellationToken ct);

    Task<ReminderDto?> GetFullAsync(Guid id, CancellationToken ct);

    Task<Reminder?> FindByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Recordatorios vencidos o próximos a vencer según fecha y km del vehículo.</summary>
    Task<IReadOnlyList<DueReminderData>> FindDueAsync(
        DateTimeOffset cutoffDate,
        int kmLookahead,
        DateTimeOffset notifiedBefore,
        CancellationToken ct);

    /// <summary>Lo mismo que <see cref="FindDueAsync"/> pero de todos los talleres (tarea periódica, sin sesión).</summary>
    Task<IReadOnlyList<DueReminderData>> FindDueAllTenantsAsync(
        DateTimeOffset cutoffDate,
        int kmLookahead,
        DateTimeOffset notifiedBefore,
        CancellationToken ct);

    Task<bool> VehicleExistsAsync(Guid vehicleId, CancellationToken ct);

    Task AddAsync(Reminder reminder, CancellationToken ct);

    Task AddNotificationAsync(Notification notification, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
