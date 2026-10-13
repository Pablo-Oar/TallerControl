using TallerControl.Domain.Catalog;

namespace TallerControl.Application.Catalog;

/// <summary>Ítem del listado de catálogo (doc 05 §2.5).</summary>
public sealed record CatalogItemListDto(
    Guid Id,
    CatalogItemKind Kind,
    string Name,
    string? Sku,
    decimal NetPrice,
    decimal VatRate,
    decimal? Cost,
    bool TrackStock,
    decimal StockQty,
    decimal? StockMin,
    bool IsActive,
    bool IsLowStock);

/// <summary>Detalle completo de un ítem (doc 05 §2.5).</summary>
public sealed record CatalogItemDto(
    Guid Id,
    CatalogItemKind Kind,
    string Name,
    string? Sku,
    decimal NetPrice,
    decimal VatRate,
    decimal? Cost,
    bool TrackStock,
    decimal StockQty,
    decimal? StockMin,
    int? DurationMinutes,
    int? BufferMinutes,
    bool IsActive,
    bool IsLowStock,
    uint Version);

/// <summary>Ítem del libro de movimientos (doc 05 §2.5).</summary>
public sealed record StockMovementDto(
    Guid Id,
    decimal Quantity,
    StockMovementReason Reason,
    string? Note,
    Guid? WorkOrderItemId,
    Guid? CreatedBy,
    DateTimeOffset CreatedAt);

/// <summary>Cuerpo de <c>POST /api/v1/catalog</c> (doc 05 §2.5).</summary>
public sealed record CreateCatalogItemRequest(
    CatalogItemKind Kind,
    string Name,
    string? Sku = null,
    decimal NetPrice = 0,
    decimal VatRate = 21,
    decimal? Cost = null,
    bool TrackStock = false,
    decimal? StockMin = null,
    int? DurationMinutes = null,
    int? BufferMinutes = null);

/// <summary>Cuerpo de <c>PUT /api/v1/catalog/{id}</c> (doc 05 §2.5). El stock <strong>no</strong> se toca acá.</summary>
public sealed record UpdateCatalogItemRequest(
    string Name,
    string? Sku = null,
    decimal NetPrice = 0,
    decimal VatRate = 21,
    decimal? Cost = null,
    decimal? StockMin = null,
    int? DurationMinutes = null,
    int? BufferMinutes = null,
    bool IsActive = true);

/// <summary>Cuerpo de <c>POST /api/v1/catalog/{id}/stock-adjustments</c> (doc 05 §2.5, T-STK-01).</summary>
public sealed record ManualStockAdjustmentRequest(
    decimal Quantity,
    string Note);
