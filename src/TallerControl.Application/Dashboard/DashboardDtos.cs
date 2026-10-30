using TallerControl.Domain.Cash;
using TallerControl.Domain.Scheduling;
using TallerControl.Domain.Work;

namespace TallerControl.Application.Dashboard;

/// <summary>
/// Resumen del Inicio (REQ-01, plan en docs/plan-dashboard-inicio.md). Los cuatro primeros campos son los del
/// dashboard original y se conservan. Las secciones que dependen de un permiso (<c>HotQuotes</c>,
/// <c>TodayAppointments</c>, <c>Cash</c>, <c>LowStock</c>) son <c>null</c> cuando el rol no lo tiene: no viajan vacías.
/// </summary>
public sealed record DashboardDto(
    decimal TotalReceivable,
    int OpenWorkOrders,
    int PendingQuotes,
    int LowStockItems,
    DashboardThresholdsDto Thresholds,
    decimal RevenueToday,
    int PaymentsToday,
    IReadOnlyList<RevenueDayDto> RevenueLast7Days,
    AgedReceivableDto AgedReceivable,
    WorkOrdersByStatusDto OrdersByStatus,
    IReadOnlyList<LiveOrderDto> LiveOrders,
    int SentQuotes,
    decimal SentQuotesTotal,
    HotQuotesDto? HotQuotes,
    TodayAppointmentsDto? TodayAppointments,
    CashStatusDto? Cash,
    IReadOnlyList<LowStockItemDto>? LowStock);

/// <summary>Qué secciones puede ver el usuario (se resuelve en el endpoint con los permisos de la sesión).</summary>
public sealed record DashboardScope(bool Quotes, bool Appointments, bool Cash, bool Stock);

/// <summary>Umbrales del taller vigentes (D-42): el cliente los muestra junto a cada alerta.</summary>
public sealed record DashboardThresholdsDto(int HotQuoteHours, int StuckOrderDays, int AgedReceivableDays);

public sealed record RevenueDayDto(DateOnly Date, decimal Amount);

/// <summary>Órdenes con saldo desde hace más de N días (D-39): cantidad y monto.</summary>
public sealed record AgedReceivableDto(int Count, decimal Amount);

/// <summary>Órdenes activas por estado; <c>Stuck</c> son las que llevan más de N días en el mismo estado.</summary>
public sealed record WorkOrdersByStatusDto(int Pending, int InProgress, int WaitingParts, int ReadyToDeliver, int Stuck);

public sealed record LiveOrderDto(
    Guid Id,
    int Number,
    WorkOrderStatus Status,
    WorkOrderPriority Priority,
    string CustomerName,
    string VehiclePlate,
    string? VehicleLabel,
    string? MechanicName,
    DateTimeOffset StatusSince,
    int DaysInStatus,
    bool IsStuck);

public sealed record HotQuotesDto(int Total, IReadOnlyList<HotQuoteDto> Items);

public sealed record HotQuoteDto(
    Guid Id,
    int Number,
    string CustomerName,
    string? CustomerPhone,
    string VehiclePlate,
    decimal Total,
    DateTimeOffset SentAt,
    int HoursSinceSent);

public sealed record TodayAppointmentsDto(int Total, int Unassigned, IReadOnlyList<TodayAppointmentDto> Items);

public sealed record TodayAppointmentDto(
    Guid Id,
    DateTimeOffset ScheduledAt,
    string CustomerName,
    string VehiclePlate,
    string? MechanicName,
    AppointmentStatus Status);

public sealed record CashStatusDto(decimal TotalBalance, IReadOnlyList<CashAccountStatusDto> Accounts);

public sealed record CashAccountStatusDto(
    Guid Id,
    string Name,
    CashAccountKind Kind,
    decimal Balance,
    DateTimeOffset? SessionOpenedAt,
    string? SessionOpenedBy);

public sealed record LowStockItemDto(Guid Id, string Name, string? Sku, decimal StockQty, decimal? StockMin);
