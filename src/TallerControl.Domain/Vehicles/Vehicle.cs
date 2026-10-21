using System.Text.RegularExpressions;
using TallerControl.Domain.Common;

namespace TallerControl.Domain.Vehicles;

/// <summary>
/// Vehículo identificado por patente. El km nunca baja sin permiso explícito (REQ-06, doc 04 §3.2).
/// </summary>
public sealed class Vehicle : Entity, ITenantOwned, IVersioned, IAudited, IAuditable, ISoftDeletable
{
    private static readonly Regex PlatePattern = new(@"^[A-Z0-9]{5,10}$", RegexOptions.Compiled);

    private Vehicle()
    {
    }

    public Vehicle(
        Guid tenantId,
        Guid customerId,
        string plate,
        string? brand = null,
        string? model = null,
        string? version = null,
        string? color = null,
        int? year = null,
        string? vin = null,
        string? fuel = null,
        int odometerKm = 0,
        VehicleUsageProfile? usageProfile = null,
        string? engineNumber = null)
    {
        TenantId = tenantId;
        CustomerId = customerId;
        Plate = NormalizePlate(plate);
        Brand = brand?.Trim();
        Model = model?.Trim();
        VersionName = version?.Trim();
        Color = color?.Trim();
        Year = year;
        Vin = vin?.Trim().ToUpperInvariant();
        Fuel = fuel?.Trim();
        OdometerKm = odometerKm >= 0 ? odometerKm
            : throw new ArgumentOutOfRangeException(nameof(odometerKm), "El km no puede ser negativo.");
        UsageProfile = usageProfile;
        EngineNumber = engineNumber?.Trim().ToUpperInvariant();
    }

    public Guid TenantId { get; private set; }
    public Guid CustomerId { get; private set; }
    public string Plate { get; private set; } = string.Empty;
    public string? Brand { get; private set; }
    public string? Model { get; private set; }
    public string? VersionName { get; private set; }
    public string? Color { get; private set; }
    public int? Year { get; private set; }
    public string? Vin { get; private set; }
    public string? Fuel { get; private set; }
    public int OdometerKm { get; private set; }
    public VehicleUsageProfile? UsageProfile { get; private set; }
    public string? EngineNumber { get; private set; }
    public uint Version { get; private set; }

    // IAuditable
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    // ISoftDeletable
    public DateTimeOffset? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }
    public string? DeleteReason { get; private set; }

    public bool IsDeleted => DeletedAt.HasValue;

    /// <summary>Actualiza los datos del vehículo. La patente no se puede cambiar en la edición básica.</summary>
    public void Update(
        string? brand,
        string? model,
        string? version,
        string? color,
        int? year,
        string? vin,
        string? fuel,
        VehicleUsageProfile? usageProfile = null,
        string? engineNumber = null)
    {
        Brand = brand?.Trim();
        Model = model?.Trim();
        VersionName = version?.Trim();
        Color = color?.Trim();
        Year = year;
        Vin = vin?.Trim().ToUpperInvariant();
        Fuel = fuel?.Trim();
        UsageProfile = usageProfile;
        EngineNumber = engineNumber?.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Actualiza el odómetro. Si el nuevo valor es mayor al actual, lo sube automáticamente.
    /// Para bajar el km se necesita permiso explícito (REQ-06).
    /// </summary>
    public void UpdateOdometer(int newKm, bool allowDecrease)
    {
        if (newKm < 0)
            throw new ArgumentOutOfRangeException(nameof(newKm), "El km no puede ser negativo.");

        if (newKm < OdometerKm && !allowDecrease)
            throw new InvalidOperationException(
                "No se puede reducir el km sin el permiso de corrección de odómetro.");

        OdometerKm = newKm;
    }

    public void Delete(string reason, Guid? deletedBy, DateTimeOffset deletedAt)
    {
        if (IsDeleted) return;
        DeletedAt = deletedAt;
        DeletedBy = deletedBy;
        DeleteReason = reason?.Trim();
    }

    public void Restore()
    {
        DeletedAt = null;
        DeletedBy = null;
        DeleteReason = null;
    }

    /// <summary>Normaliza la patente: mayúsculas y sin espacios ni guiones (REQ-06 CA-1).</summary>
    public static string NormalizePlate(string plate)
    {
        if (string.IsNullOrWhiteSpace(plate))
            throw new ArgumentException("La patente es obligatoria.", nameof(plate));

        var normalized = plate.ToUpperInvariant()
            .Replace("-", "")
            .Replace(" ", "");

        if (!PlatePattern.IsMatch(normalized))
            throw new ArgumentException(
                "La patente solo puede contener letras y números (5 a 10 caracteres).", nameof(plate));

        return normalized;
    }
}
