using TallerControl.Domain.Customers;

namespace TallerControl.Application.Customers;

/// <summary>Ítem de <c>GET /api/v1/customers</c> (doc 05 §2.3, T-CUS-01).</summary>
public sealed record CustomerListDto(
    Guid Id,
    string Name,
    string? Phone,
    string? DocNumber,
    decimal Balance);

/// <summary>Respuesta de <c>GET /api/v1/customers/{id}</c> (doc 05 §2.3, T-CUS-05).</summary>
public sealed record CustomerDto(
    Guid Id,
    string Name,
    string? Email,
    string? Phone,
    DocType? DocType,
    string? DocNumber,
    VatCondition VatCondition,
    string? Address,
    bool DoNotContact,
    string? Notes,
    decimal Balance,
    IReadOnlyList<VehicleBasicDto> Vehicles,
    uint Version);

/// <summary>Resumen de vehículo embebido en <see cref="CustomerDto"/>.</summary>
public sealed record VehicleBasicDto(Guid Id, string Plate, string? Brand, string? Model);

/// <summary>Cuerpo de <c>POST /api/v1/customers</c> y <c>PUT /api/v1/customers/{id}</c>.</summary>
public sealed record CustomerUpsertRequest(
    string Name,
    string? Email = null,
    string? Phone = null,
    DocType? DocType = null,
    string? DocNumber = null,
    VatCondition VatCondition = VatCondition.ConsumidorFinal,
    string? Address = null,
    bool DoNotContact = false,
    string? Notes = null);

/// <summary>Cuerpo de <c>DELETE /api/v1/customers/{id}</c>.</summary>
public sealed record DeleteCustomerRequest(string Reason);

/// <summary>Cuerpo de <c>POST /api/v1/customers/{id}/restore</c>.</summary>
public sealed record RestoreCustomerRequest;
