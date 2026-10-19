using TallerControl.Domain.Work;

namespace TallerControl.Application.Work;

/// <summary>Ítem del listado de órdenes (doc 05 §2.7).</summary>
public sealed record WorkOrderListDto(
    Guid Id,
    int Number,
    WorkOrderStatus Status,
    WorkOrderPriority Priority,
    Guid CustomerId,
    string CustomerName,
    Guid VehicleId,
    string VehiclePlate,
    string? VehicleBrand,
    string? VehicleModel,
    Guid? AssignedMechanicId,
    string? AssignedMechanicName,
    decimal? Total,
    decimal? Balance,
    DateTimeOffset CreatedAt);

/// <summary>Renglón de la orden (doc 05 §2.7).</summary>
public sealed record WorkOrderItemDto(
    Guid Id,
    WorkOrderItemKind Kind,
    string Description,
    Guid? CatalogItemId,
    decimal Quantity,
    decimal UnitNetPrice,
    decimal VatRate,
    decimal LineNet,
    decimal LineVat,
    decimal LineTotal,
    int Position);

/// <summary>Detalle completo de una orden (doc 05 §2.7).</summary>
public sealed record WorkOrderDto(
    Guid Id,
    int Number,
    WorkOrderStatus Status,
    WorkOrderPriority Priority,
    Guid CustomerId,
    string CustomerName,
    Guid VehicleId,
    string VehiclePlate,
    string? VehicleBrand,
    string? VehicleModel,
    Guid? AssignedMechanicId,
    string? AssignedMechanicName,
    int OdometerKm,
    int? FuelLevel,
    string? ReportedIssue,
    string? MechanicNotes,
    string? InternalNotes,
    Guid? SourceQuoteId,
    decimal? SubtotalNet,
    decimal? DiscountAmount,
    decimal? VatAmount,
    decimal? Total,
    decimal? PaidAmount,
    decimal? Balance,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? CancelledAt,
    string? CancelReason,
    IReadOnlyList<WorkOrderItemDto> Items,
    uint Version,
    DateTimeOffset CreatedAt);

/// <summary>Entrada del historial de estados (doc 05 §2.7).</summary>
public sealed record StatusChangeDto(
    Guid Id,
    WorkOrderStatus? FromStatus,
    WorkOrderStatus ToStatus,
    string? Note,
    Guid? ChangedBy,
    string? ChangedByName,
    DateTimeOffset ChangedAt);

/// <summary>Cuerpo de POST /api/v1/work-orders (doc 05 §2.7).</summary>
public sealed record CreateWorkOrderRequest(
    Guid CustomerId,
    Guid VehicleId,
    int OdometerKm,
    int? FuelLevel = null,
    string? ReportedIssue = null,
    WorkOrderPriority Priority = WorkOrderPriority.Normal,
    Guid? AssignedMechanicId = null,
    IReadOnlyList<WorkOrderItemRequest>? Items = null);

/// <summary>Renglón dentro de la creación o edición de una orden.</summary>
public sealed record WorkOrderItemRequest(
    WorkOrderItemKind Kind,
    string Description,
    Guid? CatalogItemId,
    decimal Quantity,
    decimal UnitNetPrice,
    decimal VatRate);

/// <summary>Cuerpo de PUT /api/v1/work-orders/{id} (doc 05 §2.7).</summary>
public sealed record UpdateWorkOrderRequest(
    int OdometerKm,
    int? FuelLevel,
    string? ReportedIssue,
    string? InternalNotes,
    WorkOrderPriority Priority,
    Guid? AssignedMechanicId,
    IReadOnlyList<WorkOrderItemRequest> Items);

/// <summary>Cuerpo de POST /api/v1/work-orders/{id}/status.</summary>
public sealed record ChangeStatusRequest(
    WorkOrderStatus ToStatus,
    string? Note = null,
    string? CancelReason = null);

/// <summary>
/// Cuerpo de POST /api/v1/work-orders/{id}/assign. <c>MechanicId</c> es la membresía del mecánico; el mecánico
/// puede omitirlo (se asigna a sí mismo, doc 05: "assignee = self"), dueño y administrativo lo indican.
/// </summary>
public sealed record AssignMechanicRequest(Guid? MechanicId = null);

/// <summary>Mecánico activo al que se puede asignar una orden (GET /api/v1/work-orders/mechanics).</summary>
public sealed record AssignableMechanicDto(Guid MembershipId, string DisplayName);

/// <summary>Cuerpo de PATCH /api/v1/work-orders/{id}/mechanic-notes.</summary>
public sealed record UpdateMechanicNotesRequest(string? MechanicNotes);

/// <summary>Link público de una OT para compartir con el cliente (doc 05 §2.8).</summary>
public sealed record WorkOrderPublicLinkDto(string TokenBase64Url, DateTimeOffset ExpiresAt);
