using System.Security.Cryptography;
using System.Text;
using TallerControl.Application.Common;
using TallerControl.Domain.Qr;

namespace TallerControl.Application.Qr;

public sealed class ScanQrCodeHandler(IQrRepository repo, TimeProvider clock)
{
    public async Task<ScanResultDto> HandleAsync(
        string code,
        string? ip,
        string? userAgent,
        CancellationToken ct)
    {
        var normalizedCode = code.ToUpperInvariant();
        var codeHash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedCode));
        var card = await repo.FindByCodeAsync(normalizedCode, ct);

        string outcome;

        if (card is null || card.Status == QrCardStatus.Revoked)
        {
            outcome = card is null ? "not_found" : "revoked";
            await LogEventAsync(null, null, codeHash, QrScanEventType.Scan, outcome, ip, userAgent, ct);
            // Respuesta uniforme: no revelar si la tarjeta existe pero está revocada
            throw new NotFoundException();
        }

        outcome = card.Status.ToString().ToLowerInvariant();
        await LogEventAsync(card.Id, card.TenantId, codeHash, QrScanEventType.Scan, outcome, ip, userAgent, ct);

        var workshop = await repo.GetWorkshopInfoByCardIdAsync(card.Id, ct);

        return new ScanResultDto(
            Workshop: workshop ?? new WorkshopInfoDto("Taller", null),
            CardState: outcome);
    }

    private async Task LogEventAsync(
        Guid? qrCardId,
        Guid? tenantId,
        byte[] codeHash,
        QrScanEventType type,
        string outcome,
        string? ip,
        string? userAgent,
        CancellationToken ct)
    {
        var scanEvent = new QrScanEvent(tenantId, qrCardId, codeHash, type, outcome,
            TruncateIp(ip), Truncate(userAgent, 200), clock.GetUtcNow());
        await repo.AddScanEventAsync(scanEvent, ct);
    }

    private static string? TruncateIp(string? ip)
    {
        if (ip is null) return null;
        var lastDot = ip.LastIndexOf('.');
        return lastDot > 0 ? ip[..lastDot] + ".0" : ip;
    }

    private static string? Truncate(string? s, int max) =>
        s is null ? null : s.Length <= max ? s : s[..max];
}
