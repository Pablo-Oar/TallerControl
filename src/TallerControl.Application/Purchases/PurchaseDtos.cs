using TallerControl.Domain.Purchases;

namespace TallerControl.Application.Purchases;

public sealed record PurchaseItemDto(
    Guid Id,
    Guid? CatalogItemId,
    string Description,
    decimal Quantity,
    decimal UnitCost,
    decimal VatRate,
    decimal Total
);

public sealed record PurchaseDto(
    Guid Id,
    Guid BranchId,
    Guid SupplierId,
    int DocumentNumber,
    DateTimeOffset InvoiceDate,
    int? PointOfSale,
    int? InvoiceNumber,
    decimal SubtotalNet,
    decimal VatAmount,
    decimal Perceptions,
    decimal Retentions,
    decimal Total,
    bool IsExcludedFromCiti,
    PurchaseStatus Status,
    Guid? AccountId,
    IReadOnlyList<PurchaseItemDto> Items,
    uint Version,
    string? CancelReason = null
);

public sealed record CreatePurchaseItemRequest(
    Guid? CatalogItemId,
    string Description,
    decimal Quantity,
    decimal UnitCost,
    decimal VatRate
);

public sealed record CreatePurchaseRequest(
    Guid SupplierId,
    DateTimeOffset InvoiceDate,
    int? PointOfSale,
    int? InvoiceNumber,
    decimal Perceptions,
    decimal Retentions,
    IReadOnlyList<CreatePurchaseItemRequest> Items
);

public sealed record UpdatePurchaseRequest(
    DateTimeOffset InvoiceDate,
    int? PointOfSale,
    int? InvoiceNumber,
    decimal Perceptions,
    decimal Retentions,
    IReadOnlyList<CreatePurchaseItemRequest> Items,
    uint Version
);

public sealed record CancelPurchaseRequest(string? Reason);

public sealed record ConfirmPurchaseRequest(
    Guid? AccountId,
    uint Version
);
