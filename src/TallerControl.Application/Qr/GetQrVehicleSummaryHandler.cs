using TallerControl.Application.Common;

namespace TallerControl.Application.Qr;

/// <summary>
/// Panel: busca el vehículo asociado a un código QR dentro del taller autenticado (T-QR-25).
/// El tenant filter de EF garantiza aislamiento — no requiere verificación adicional.
/// </summary>
public sealed class GetQrVehicleSummaryHandler(IQrRepository repo)
{
    public async Task<QrVehicleSummaryDto> HandleAsync(string code, CancellationToken ct)
    {
        var dto = await repo.GetVehicleSummaryByCodeAsync(code.ToUpperInvariant(), ct);
        if (dto is null)
            throw new NotFoundException();
        return dto;
    }
}
