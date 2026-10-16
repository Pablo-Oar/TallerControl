using System.Security.Cryptography;
using System.Text;
using TallerControl.Application.Common;
using TallerControl.Domain.Qr;

namespace TallerControl.Application.Qr;

/// <summary>
/// Alguien encontró llaves con una tarjeta QR y lo reporta al taller (T-QR-26..30).
/// Siempre devuelve 202 para no revelar el estado de la tarjeta.
/// </summary>
public sealed class ReportFoundKeysHandler(
    IQrRepository repo,
    IOutbox outbox,
    TimeProvider clock)
{
    public async Task HandleAsync(string code, ReportFoundKeysRequest request, string? ip, CancellationToken ct)
    {
        var normalizedCode = code.ToUpperInvariant();
        var codeHash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedCode));
        var card = await repo.FindByCodeAsync(normalizedCode, ct);

        if (card is not null && (card.Status == QrCardStatus.Assigned || card.Status == QrCardStatus.Lost))
        {
            var scanEvent = new QrScanEvent(
                card.TenantId, card.Id, codeHash,
                QrScanEventType.FoundKeys, "reported",
                TruncateIp(ip), null, clock.GetUtcNow());

            await repo.AddScanEventAsync(scanEvent, ct);

            outbox.Enqueue("qr.found_keys_alert", new
            {
                TenantId = card.TenantId,
                QrCardId = card.Id,
                Code = normalizedCode,
                ReporterName = request.ReporterName,
                ReporterPhone = request.ReporterPhone,
                ReportedAt = clock.GetUtcNow()
            });

            await repo.SaveChangesAsync(ct);
        }

        // Respuesta uniforme: no se informa si la tarjeta existe ni su estado
    }

    private static string? TruncateIp(string? ip)
    {
        if (ip is null) return null;
        var lastDot = ip.LastIndexOf('.');
        return lastDot > 0 ? ip[..lastDot] + ".0" : ip;
    }
}
