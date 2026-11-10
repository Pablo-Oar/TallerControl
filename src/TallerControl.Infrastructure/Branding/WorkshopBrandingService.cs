using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TallerControl.Application.Common;
using TallerControl.Application.Settings;
using TallerControl.Domain.Tenancy;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Branding;

/// <summary>
/// Marca del taller para documentos y superficies públicas. Se lee con <c>IgnoreQueryFilters</c> porque las
/// páginas públicas y los emails no tienen sesión; el <c>tenantId</c> siempre sale del propio documento, nunca
/// del cliente.
/// </summary>
internal sealed class WorkshopBrandingService : IWorkshopBranding
{
    private static readonly TimeSpan UrlLifetime = TimeSpan.FromMinutes(10);

    private readonly AppDbContext _db;
    private readonly IObjectStorage _storage;
    private readonly ILogger<WorkshopBrandingService> _logger;

    public WorkshopBrandingService(AppDbContext db, IObjectStorage storage, ILogger<WorkshopBrandingService> logger)
    {
        _db = db;
        _storage = storage;
        _logger = logger;
    }

    public async Task<WorkshopBranding> GetPublicAsync(Guid tenantId, CancellationToken ct)
    {
        var name = await _db.Set<Tenant>().AsNoTracking().IgnoreQueryFilters()
            .Where(t => t.Id == tenantId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(ct) ?? "Taller";

        var key = await LogoKeyAsync(tenantId, ct);
        return new WorkshopBranding(
            name,
            key is null ? null : _storage.CreatePresignedGetUrl(key, UrlLifetime));
    }

    public async Task<LogoImage?> GetLogoAsync(Guid tenantId, CancellationToken ct)
    {
        try
        {
            var key = await LogoKeyAsync(tenantId, ct);
            if (key is null) return null;

            var bytes = await _storage.ReadAllAsync(key, LogoPolicy.MaxSizeBytes, ct);
            if (bytes is null || !LogoPolicy.MatchesMagicBytes(bytes.AsSpan(0, Math.Min(bytes.Length, LogoPolicy.MagicBytesLength))))
                return null;

            return new LogoImage(bytes, ContentTypeOf(bytes));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // El logo nunca debe impedir generar un documento.
            _logger.LogWarning(ex, "No se pudo leer el logo del taller {TenantId}", tenantId);
            return null;
        }
    }

    private Task<string?> LogoKeyAsync(Guid tenantId, CancellationToken ct) =>
        _db.Set<TenantSettings>().AsNoTracking().IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId)
            .Select(s => s.LogoFileKey)
            .FirstOrDefaultAsync(ct);

    private static string ContentTypeOf(byte[] b) =>
        b.Length >= 4 && b[0] == 0x89 && b[1] == 0x50 ? "image/png"
        : b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 ? "image/jpeg"
        : "image/webp";
}
