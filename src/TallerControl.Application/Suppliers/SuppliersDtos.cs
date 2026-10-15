using TallerControl.Domain.Customers;

namespace TallerControl.Application.Suppliers;

public sealed record SupplierDto(
    Guid Id,
    string Name,
    string? Cuit,
    VatCondition VatCondition,
    string? Address,
    string? Phone,
    string? Email,
    string? Notes,
    uint Version
);

public sealed record CreateSupplierRequest(
    string Name,
    string? Cuit,
    VatCondition VatCondition,
    string? Address,
    string? Phone,
    string? Email,
    string? Notes
);

public sealed record UpdateSupplierRequest(
    string Name,
    string? Cuit,
    VatCondition VatCondition,
    string? Address,
    string? Phone,
    string? Email,
    string? Notes,
    uint Version
);
