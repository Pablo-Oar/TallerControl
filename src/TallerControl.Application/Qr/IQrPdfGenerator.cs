namespace TallerControl.Application.Qr;

public interface IQrPdfGenerator
{
    Task<byte[]> GenerateCardAsync(Guid qrCardId, CancellationToken ct);

    /// <summary>Hoja A4 con los QR de las tarjetas libres del taller (para imprimir un lote y recortar).</summary>
    Task<byte[]> GenerateFreeSheetAsync(int maxCards, CancellationToken ct);
}
