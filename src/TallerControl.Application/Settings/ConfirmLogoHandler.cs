using TallerControl.Application.Common;

namespace TallerControl.Application.Settings;

/// <summary>
/// Caso de uso: confirmar que el logo ya se subió a R2/RustFS (doc 03 §flujo de adjuntos, doc 07 §5).
/// Relee el objeto con HEAD y valida los magic bytes antes de persistir <c>logo_file_key</c>;
/// el <c>Content-Type</c> declarado en el paso anterior no alcanza por sí solo.
/// </summary>
public sealed class ConfirmLogoHandler
{
    private readonly ISettingsRepository _repo;
    private readonly IObjectStorage _storage;

    public ConfirmLogoHandler(ISettingsRepository repo, IObjectStorage storage)
    {
        _repo = repo;
        _storage = storage;
    }

    public async Task<SettingsDto> HandleAsync(CancellationToken ct)
    {
        var tenant = await _repo.FindTenantAsync(ct) ?? throw new NotFoundException();
        var settings = await _repo.FindSettingsAsync(ct) ?? throw new NotFoundException();

        var fileKey = LogoPolicy.FileKeyFor(tenant.Id);

        var stored = await _storage.HeadAsync(fileKey, ct)
            ?? throw new BusinessRuleException(
                "No se encontró el logo subido.",
                new Dictionary<string, string[]> { ["logo"] = ["Pedí una URL de subida y volvé a intentar."] });

        if (stored.Size <= 0 || stored.Size > LogoPolicy.MaxSizeBytes
            || stored.ContentType is null || !LogoPolicy.AllowedContentTypes.Contains(stored.ContentType))
        {
            throw new BusinessRuleException(
                "El objeto subido no coincide con el tipo o tamaño esperado.",
                new Dictionary<string, string[]> { ["logo"] = ["Tipo o tamaño distintos a los declarados."] });
        }

        var prefix = await _storage.ReadPrefixAsync(fileKey, LogoPolicy.MagicBytesLength, ct);
        if (!LogoPolicy.MatchesMagicBytes(prefix))
        {
            throw new BusinessRuleException(
                "El contenido del archivo no corresponde a una imagen válida.",
                new Dictionary<string, string[]> { ["logo"] = ["Los bytes del archivo no coinciden con el tipo declarado."] });
        }

        settings.SetLogo(fileKey);
        await _repo.SaveChangesAsync(ct);

        return SettingsMapper.ToDto(tenant, settings, _storage);
    }
}
