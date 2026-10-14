using TallerControl.Domain.Quotes;

namespace TallerControl.Application.PublicLinks;

/// <summary>
/// Vista pública de un presupuesto (sin datos internos ni costos, doc 05 §2.8, T-PUB-01).
/// </summary>
public sealed record PublicQuoteDto(
    Guid Id,
    int Number,
    QuoteStatus Status,
    string CustomerName,
    string VehiclePlate,
    string? VehicleBrand,
    string? VehicleModel,
    decimal GlobalDiscountPct,
    decimal SubtotalNet,
    decimal DiscountAmount,
    decimal VatAmount,
    decimal Total,
    DateOnly ValidUntil,
    string? CustomerNotes,
    IReadOnlyList<PublicQuoteItemDto> Items,
    /// <summary>Hash SHA-256 del contenido del presupuesto, en Base64. Requerido para aprobar.</summary>
    string ContentHash,
    DateTimeOffset CreatedAt);

/// <summary>Renglón visible al cliente (sin costo, doc 05 §2.8).</summary>
public sealed record PublicQuoteItemDto(
    Guid Id,
    QuoteItemKind Kind,
    string Description,
    decimal Quantity,
    decimal UnitNetPrice,
    decimal VatRate,
    decimal LineNet,
    decimal LineVat,
    decimal LineTotal,
    int Position,
    bool RequiredForApproval);

/// <summary>Cuerpo de POST /api/public/v1/quotes/{token}/response (doc 05 §2.8, T-PUB-02..06).</summary>
public sealed record SubmitPublicResponseRequest(
    string Decision,
    string SignerName,
    string ContentHash,
    string? Comment = null,
    IReadOnlyList<PublicResponseItemRequest>? Items = null);

/// <summary>Decisión por renglón dentro de la respuesta pública.</summary>
public sealed record PublicResponseItemRequest(Guid QuoteItemId, bool Approved);

/// <summary>Resultado de enviar la respuesta al presupuesto.</summary>
public sealed record SubmitPublicResponseResult(
    string Decision,
    decimal ApprovedTotal,
    QuoteStatus NewStatus);

/// <summary>Vista pública de una orden de trabajo (sin precios, doc 05 §2.8).</summary>
public sealed record PublicWorkOrderDto(
    Guid Id,
    int Number,
    string Status,
    string CustomerName,
    string VehiclePlate,
    string? VehicleBrand,
    string? VehicleModel,
    string? AssignedMechanicName,
    string? ReportedIssue,
    string? MechanicNotes,
    DateTimeOffset CreatedAt);

/// <summary>Tipos de token soportados para lookup público.</summary>
public enum PublicTokenTargetType
{
    Quote,
    WorkOrder,
}
