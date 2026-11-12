using System.Security.Cryptography;
using System.Text;
using TallerControl.Domain.Qr;

namespace TallerControl.Application.Qr;

/// <summary>Registra la actividad de una tarjeta (la que ve el taller en «Actividad»). Quien llama guarda los cambios.</summary>
internal static class QrActivityLog
{
    public static Task AddAsync(
        IQrRepository repo, TimeProvider clock, Guid tenantId, Guid qrCardId, string code,
        QrScanEventType type, string outcome, string? ip, CancellationToken ct)
    {
        var codeHash = SHA256.HashData(Encoding.UTF8.GetBytes(code.ToUpperInvariant()));
        var scanEvent = new QrScanEvent(tenantId, qrCardId, codeHash, type, outcome, TruncateIp(ip), null, clock.GetUtcNow());
        return repo.AddScanEventAsync(scanEvent, ct);
    }

    private static string? TruncateIp(string? ip)
    {
        if (ip is null) return null;
        var lastDot = ip.LastIndexOf('.');
        return lastDot > 0 ? ip[..lastDot] + ".0" : ip;
    }
}
