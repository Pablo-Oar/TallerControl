using TallerControl.Application.Common;
using TallerControl.Domain.Vehicles;

namespace TallerControl.Application.Vehicles;

public interface IVehicleRepository
{
    Task<Page<VehicleListDto>> SearchAsync(string? q, int page, int pageSize, SortSpec sort, bool includeDeleted, CancellationToken ct);

    Task<VehicleDto?> GetFullAsync(Guid id, CancellationToken ct);

    Task<Vehicle?> FindByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Busca también entre los borrados, para restaurar.</summary>
    Task<Vehicle?> FindAnyByIdAsync(Guid id, CancellationToken ct);

    Task<bool> PlateExistsAsync(Guid? excludeId, string normalizedPlate, CancellationToken ct);

    Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken ct);

    /// <summary>¿El vehículo tiene órdenes de trabajo abiertas? (implementado en E4; devuelve false en E3).</summary>
    Task<bool> HasOpenWorkOrdersAsync(Guid vehicleId, CancellationToken ct);

    /// <summary>
    /// Historial del vehículo: órdenes y presupuestos por fecha descendente, con km y total (REQ-07, T-VEH-08).
    /// <paramref name="includePrices"/> en false (rol sin prices.view) deja el total en null: no viaja.
    /// </summary>
    Task<Page<HistoryEntryDto>> GetHistoryAsync(Guid vehicleId, bool includePrices, int page, int pageSize, CancellationToken ct);

    Task AddAsync(Vehicle vehicle, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
