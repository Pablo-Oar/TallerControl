using TallerControl.Application.Common;

namespace TallerControl.Application.Settings;

/// <summary>Caso de uso: leer la configuración del taller (doc 05 §2.2, T-SET-01).</summary>
public sealed class GetSettingsHandler
{
    private readonly ISettingsRepository _repo;
    private readonly IObjectStorage _storage;

    public GetSettingsHandler(ISettingsRepository repo, IObjectStorage storage)
    {
        _repo = repo;
        _storage = storage;
    }

    public async Task<SettingsDto> HandleAsync(CancellationToken ct)
    {
        var tenant = await _repo.FindTenantAsync(ct) ?? throw new NotFoundException();
        var settings = await _repo.FindSettingsAsync(ct) ?? throw new NotFoundException();

        return SettingsMapper.ToDto(tenant, settings, _storage);
    }
}
