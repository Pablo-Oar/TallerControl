namespace TallerControl.Application.Qr;

/// <summary>
/// Payload de la cookie tc_qr firmada por Data Protection API.
/// Representa la sesión del dueño del vehículo tras verificación OTP.
/// </summary>
public sealed record QrSession(
    Guid QrCardId,
    Guid VehicleId,
    Guid CustomerId,
    Guid TenantId,
    DateTimeOffset IssuedAt);
