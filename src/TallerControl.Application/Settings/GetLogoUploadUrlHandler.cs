using TallerControl.Application.Common;

namespace TallerControl.Application.Settings;

/// <summary>
/// Caso de uso: pedir la URL para subir el logo (doc 05 §2.2, REQ-04 V2, T-SET-03).
/// Valida tamaño y tipo declarados; la clave es determinística por taller, nunca llega del cliente
/// (doc 07 §5). La subida no queda confirmada hasta <see cref="ConfirmLogoHandler"/>.
/// </summary>
public sealed class GetLogoUploadUrlHandler
{
    private static readonly TimeSpan PutExpiry = TimeSpan.FromMinutes(5);

    private readonly ISettingsRepository _repo;
    private readonly IObjectStorage _storage;

    public GetLogoUploadUrlHandler(ISettingsRepository repo, IObjectStorage storage)
    {
        _repo = repo;
        _storage = storage;
    }

    public async Task<LogoUploadUrlResponse> HandleAsync(LogoUploadUrlRequest request, CancellationToken ct)
    {
        var tenant = await _repo.FindTenantAsync(ct) ?? throw new NotFoundException();

        if (!LogoPolicy.AllowedContentTypes.Contains(request.ContentType))
        {
            throw new BusinessRuleException(
                "Tipo de archivo no permitido para el logo.",
                new Dictionary<string, string[]> { ["contentType"] = ["Debe ser PNG, JPG o WebP."] });
        }

        if (request.Size <= 0 || request.Size > LogoPolicy.MaxSizeBytes)
        {
            throw new BusinessRuleException(
                "El logo supera el tamaño máximo permitido.",
                new Dictionary<string, string[]> { ["size"] = ["Máximo 5 MB."] });
        }

        var fileKey = LogoPolicy.FileKeyFor(tenant.Id);
        var uploadUrl = _storage.CreatePresignedPutUrl(fileKey, request.ContentType, PutExpiry);

        return new LogoUploadUrlResponse(uploadUrl, fileKey);
    }
}
