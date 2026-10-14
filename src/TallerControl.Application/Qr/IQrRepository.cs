using TallerControl.Application.Common;
using TallerControl.Domain.Qr;

namespace TallerControl.Application.Qr;

public interface IQrRepository
{
    Task<QrCard?> FindByIdAsync(Guid id, CancellationToken ct);
    Task<QrCard?> FindByCodeAsync(string code, CancellationToken ct);
    Task<QrCard?> FindActiveByVehicleAsync(Guid vehicleId, CancellationToken ct);
    Task<QrCardDto?> GetCardDtoAsync(Guid id, CancellationToken ct);
    Task<Page<QrCardListDto>> SearchAsync(QrCardStatus? status, string? q, int page, int pageSize, CancellationToken ct);
    Task<Page<QrEventDto>> GetEventsAsync(Guid qrCardId, int page, int pageSize, CancellationToken ct);
    Task AddRangeAsync(IEnumerable<QrCard> cards, CancellationToken ct);
    Task AddScanEventAsync(QrScanEvent scanEvent, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);

    // Métodos para superficies públicas (IgnoreQueryFilters en la implementación)
    Task<WorkshopInfoDto?> GetWorkshopInfoByCardIdAsync(Guid qrCardId, CancellationToken ct);
    Task<VehicleOwnerInfo?> GetVehicleOwnerForChallengeAsync(Guid vehicleId, CancellationToken ct);
    Task<OwnerVehicleDto?> GetOwnerVehicleDtoAsync(Guid vehicleId, Guid tenantId, CancellationToken ct);
}

/// <summary>Datos del vehículo y su dueño necesarios para crear el desafío OTP.</summary>
public record VehicleOwnerInfo(
    string NormalizedPlate,
    Guid CustomerId,
    Guid TenantId,
    string? Email,
    byte[]? OwnerAccessCodeHash);
