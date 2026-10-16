namespace TallerControl.Application.Vehicles;

/// <summary>Ítem de <c>GET /api/v1/vehicles</c> (doc 05 §2.3, T-VEH-01).</summary>
public sealed record VehicleListDto(
    Guid Id,
    string Plate,
    string? Brand,
    string? Model,
    int? Year,
    string CustomerName);

/// <summary>Respuesta de <c>GET /api/v1/vehicles/{id}</c> (doc 05 §2.3, T-VEH-05).</summary>
public sealed record VehicleDto(
    Guid Id,
    string Plate,
    Guid CustomerId,
    string CustomerName,
    string? Brand,
    string? Model,
    string? VersionName,
    string? Color,
    int? Year,
    string? Vin,
    string? Fuel,
    int OdometerKm,
    string? UsageProfile,
    string? EngineNumber,
    QrCardBasicDto? ActiveQrCard,
    uint Version);

/// <summary>
/// Resumen de tarjeta QR activa. <c>null</c> hasta que se implemente QR (E8).
/// </summary>
public sealed record QrCardBasicDto(Guid Id, string Code);

/// <summary>Ítem del historial del vehículo (doc 05 §2.3, T-VEH-08).</summary>
public sealed record HistoryEntryDto(
    string Type,
    Guid Id,
    string Number,
    DateTimeOffset Date,
    int? OdometerKm,
    string Status,
    decimal? Total);

/// <summary>Cuerpo de <c>POST /api/v1/vehicles</c> y <c>PUT /api/v1/vehicles/{id}</c>.</summary>
public sealed record VehicleUpsertRequest(
    string Plate,
    Guid CustomerId,
    string? Brand = null,
    string? Model = null,
    string? Version = null,
    string? Color = null,
    int? Year = null,
    string? Vin = null,
    string? Fuel = null,
    int OdometerKm = 0,
    TallerControl.Domain.Vehicles.VehicleUsageProfile? UsageProfile = null,
    string? EngineNumber = null);

/// <summary>Cuerpo de <c>DELETE /api/v1/vehicles/{id}</c>.</summary>
public sealed record DeleteVehicleRequest(string Reason);
