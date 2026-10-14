namespace TallerControl.Application.Qr;

public interface IQrPdfGenerator
{
    Task<byte[]> GenerateCardAsync(Guid qrCardId, CancellationToken ct);
}
