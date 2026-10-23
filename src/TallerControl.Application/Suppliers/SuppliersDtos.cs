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
    uint Version,
    // Compras vigentes (borrador o confirmadas) del proveedor: el detalle lo informa para avisar antes de darlo de baja.
    int PurchaseCount = 0
);

public sealed record DeleteSupplierRequest(string? Reason);

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
