using TallerControl.Domain.CounterSales;

namespace TallerControl.Application.CounterSales;

public record CounterSaleItemDto(
    Guid Id,
    Guid? CatalogItemId,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal VatRate,
    decimal NetAmount,
    decimal VatAmount,
    decimal Total);

public record CounterSaleDto(
    Guid Id,
    Guid BranchId,
    Guid? CustomerId,
    int DocumentNumber,
    DateTimeOffset Date,
    decimal SubtotalNet,
    decimal VatAmount,
    decimal Total,
    CounterSaleStatus Status,
    Guid? AccountId,
    IReadOnlyList<CounterSaleItemDto> Items,
    uint Version);

public record CreateCounterSaleItemRequest(
    Guid? CatalogItemId,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal VatRate);

public record CreateCounterSaleRequest(
    Guid? CustomerId,
    IList<CreateCounterSaleItemRequest> Items);

public record ConfirmCounterSaleRequest(Guid? AccountId, uint Version);
