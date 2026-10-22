using TallerControl.Domain.Reminders;

namespace TallerControl.Application.Reminders;

/// <summary>Ítem de la lista de recordatorios.</summary>
public sealed record ReminderListDto(
    Guid Id,
    Guid VehicleId,
    string VehiclePlate,
    ReminderKind Kind,
    string Title,
    DateTimeOffset? DueDate,
    int? DueKm,
    DateTimeOffset? LastNotifiedAt,
    DateTimeOffset? CompletedAt);

/// <summary>Detalle completo de un recordatorio.</summary>
public sealed record ReminderDto(
    Guid Id,
    Guid VehicleId,
    string VehiclePlate,
    ReminderKind Kind,
    string Title,
    string? Notes,
    DateTimeOffset? DueDate,
    int? DueKm,
    DateTimeOffset? LastNotifiedAt,
    uint Version,
    DateTimeOffset? CompletedAt);

/// <summary>Cuerpo de POST y PUT /api/v1/reminders.</summary>
public sealed record UpsertReminderRequest(
    Guid VehicleId,
    ReminderKind Kind,
    string Title,
    DateTimeOffset? DueDate = null,
    int? DueKm = null,
    string? Notes = null);

/// <summary>Resultado de la evaluación de recordatorios vencidos.</summary>
public sealed record EvaluateRemindersResult(int NotificationsCreated);

/// <summary>Datos de un recordatorio vencido para crear la notificación (projection de la consulta JOIN).</summary>
public sealed record DueReminderData(
    Domain.Reminders.Reminder Entity,
    string VehiclePlate,
    string CustomerName,
    string? CustomerEmail = null,
    bool CustomerDoNotContact = false);
