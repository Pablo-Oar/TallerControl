using TallerControl.Application.Common;
using TallerControl.Domain.Notifications;

namespace TallerControl.Application.Reminders;

/// <summary>
/// Evalúa los recordatorios del taller actual y crea notificaciones de panel para los vencidos
/// o próximos a vencer. Se llama manualmente desde el endpoint POST /api/v1/reminders/evaluate (T-REM-02).
/// </summary>
public sealed class EvaluateDueRemindersHandler(
    IReminderRepository repo,
    IOutbox outbox,
    ITenantContext tenant,
    TimeProvider clock)
{
    private const int KmLookahead = 500;
    private const int DaysAhead = 7;
    private const int CooldownDays = 7;

    public async Task<EvaluateRemindersResult> HandleAsync(CancellationToken ct)
    {
        _ = tenant.TenantId ?? throw new InvalidOperationException("No hay taller en la sesión.");

        var now = clock.GetUtcNow();
        var cutoffDate = now.AddDays(DaysAhead);
        var notifiedBefore = now.AddDays(-CooldownDays);

        var due = await repo.FindDueAsync(cutoffDate, KmLookahead, notifiedBefore, ct);

        foreach (var item in due)
        {
            var label = item.Entity.DueDate.HasValue
                ? $"el {item.Entity.DueDate.Value:dd/MM/yyyy}"
                : $"a los {item.Entity.DueKm} km";

            var notification = new Notification(
                tenantId: item.Entity.TenantId,
                type: "reminder.due",
                title: $"{item.Entity.Title} — {item.VehiclePlate} vence {label}",
                createdAt: now,
                link: $"/app/vehicles/{item.Entity.VehicleId}");

            await repo.AddNotificationAsync(notification, ct);

            outbox.Enqueue("reminder.due", new
            {
                tenantId = item.Entity.TenantId,
                vehiclePlate = item.VehiclePlate,
                customerName = item.CustomerName,
                title = item.Entity.Title,
                dueDate = item.Entity.DueDate,
                dueKm = item.Entity.DueKm,
            });

            item.Entity.MarkNotified(now);
        }

        if (due.Count > 0)
            await repo.SaveChangesAsync(ct);

        return new EvaluateRemindersResult(due.Count);
    }
}
