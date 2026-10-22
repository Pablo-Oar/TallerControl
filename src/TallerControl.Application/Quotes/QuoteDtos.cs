using TallerControl.Domain.Quotes;

namespace TallerControl.Application.Quotes;

/// <summary>Ítem del listado de presupuestos (doc 05 §2.6).</summary>
public sealed record QuoteListDto(
    Guid Id,
    int Number,
    QuoteStatus Status,
    Guid CustomerId,
    string CustomerName,
    Guid VehicleId,
    string VehiclePlate,
    decimal Total,
    decimal GlobalDiscountPct,
    DateOnly ValidUntil,
    DateTimeOffset CreatedAt);

/// <summary>Renglón de presupuesto (doc 05 §2.6).</summary>
public sealed record QuoteItemDto(
    Guid Id,
    QuoteItemKind Kind,
    string Description,
    Guid? CatalogItemId,
    decimal Quantity,
    decimal UnitNetPrice,
    decimal VatRate,
    decimal LineNet,
    decimal LineVat,
    decimal LineTotal,
    int Position,
    bool RequiredForApproval);

/// <summary>Detalle completo de un presupuesto (doc 05 §2.6).</summary>
public sealed record QuoteDto(
    Guid Id,
    int Number,
    QuoteStatus Status,
    Guid CustomerId,
    string CustomerName,
    Guid VehicleId,
    string VehiclePlate,
    string? VehicleBrand,
    string? VehicleModel,
    decimal GlobalDiscountPct,
    decimal SubtotalNet,
    decimal DiscountAmount,
    decimal VatAmount,
    decimal Total,
    decimal? ApprovedTotal,
    DateOnly ValidUntil,
    string? CustomerNotes,
    string? InternalNotes,
    Guid? WorkOrderId,
    DateTimeOffset? SentAt,
    DateTimeOffset? ConvertedAt,
    string? CancelReason,
    IReadOnlyList<QuoteItemDto> Items,
    uint Version,
    DateTimeOffset CreatedAt);

/// <summary>Respuesta histórica del cliente a un presupuesto.</summary>
public sealed record QuoteResponseDto(
    Guid Id,
    string Decision,
    string SignerName,
    decimal ApprovedTotal,
    string? Comment,
    bool IsCurrent,
    DateTimeOffset CreatedAt,
    IReadOnlyList<QuoteResponseItemDto> Items,
    /// <summary>Clave interna de la firma en el almacenamiento: nunca viaja al cliente web.</summary>
    [property: System.Text.Json.Serialization.JsonIgnore] string? SignatureKey = null,
    bool HasSignerDocument = false,
    /// <summary>URL prefirmada (10 min) de la imagen de la firma, si el cliente firmó.</summary>
    string? SignatureUrl = null);

public sealed record SignerDocumentDto(string DocNumber);

/// <summary>Decisión por renglón en una respuesta.</summary>
public sealed record QuoteResponseItemDto(Guid QuoteItemId, bool Approved);

/// <summary>Cuerpo de POST /api/v1/quotes (doc 05 §2.6).</summary>
public sealed record CreateQuoteRequest(
    Guid CustomerId,
    Guid VehicleId,
    DateOnly? ValidUntil,
    decimal GlobalDiscountPct = 0,
    string? CustomerNotes = null,
    string? InternalNotes = null,
    IReadOnlyList<QuoteItemRequest>? Items = null);

/// <summary>Renglón dentro de creación o edición de presupuesto.</summary>
public sealed record QuoteItemRequest(
    QuoteItemKind Kind,
    string Description,
    Guid? CatalogItemId,
    decimal Quantity,
    decimal UnitNetPrice,
    decimal VatRate,
    bool RequiredForApproval = false);

/// <summary>Cuerpo de PUT /api/v1/quotes/{id} (doc 05 §2.6).</summary>
public sealed record UpdateQuoteRequest(
    Guid CustomerId,
    Guid VehicleId,
    DateOnly ValidUntil,
    decimal GlobalDiscountPct,
    string? CustomerNotes,
    string? InternalNotes,
    IReadOnlyList<QuoteItemRequest> Items);

/// <summary>Cuerpo de POST /api/v1/quotes/{id}/cancel.</summary>
public sealed record CancelQuoteRequest(string Reason);

/// <summary>Cuerpo de POST /api/v1/quotes/{id}/reopen.</summary>
public sealed record ReopenQuoteRequest(string? Reason = null);

/// <summary>Cuerpo de POST /api/v1/quotes/{id}/convert (doc 05 §2.6, T-QUO-09).</summary>
public sealed record ConvertQuoteRequest(
    int OdometerKm,
    int? FuelLevel = null,
    string? ReportedIssue = null);

/// <summary>Cuerpo de DELETE /api/v1/quotes/{id}.</summary>
public sealed record DeleteQuoteRequest(string? Reason = null);
