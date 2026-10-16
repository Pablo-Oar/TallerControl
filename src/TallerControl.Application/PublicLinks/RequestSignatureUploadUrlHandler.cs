using TallerControl.Application.Common;

namespace TallerControl.Application.PublicLinks;

/// <summary>
/// Caso de uso: genera una URL PUT prefirmada para subir la firma del cliente (RF-R4, F-16).
/// El token del presupuesto se valida para evitar abusos, pero no se requiere sesión.
/// La clave de objeto la genera el servidor; nunca llega del cliente.
/// </summary>
public sealed class RequestSignatureUploadUrlHandler
{
    private static readonly TimeSpan PutExpiry = TimeSpan.FromMinutes(5);

    private readonly IPublicLinkRepository _linkRepo;
    private readonly IObjectStorage _storage;
    private readonly TimeProvider _clock;

    public RequestSignatureUploadUrlHandler(
        IPublicLinkRepository linkRepo,
        IObjectStorage storage,
        TimeProvider clock)
    {
        _linkRepo = linkRepo;
        _storage = storage;
        _clock = clock;
    }

    public async Task<SignatureUploadUrlResponse> HandleAsync(
        string tokenBase64Url,
        SignatureUploadUrlRequest request,
        CancellationToken ct)
    {
        // Validar que el enlace público existe y está activo.
        var tokenHash = GetPublicQuoteHandler.HashToken(tokenBase64Url);
        var now = _clock.GetUtcNow();
        var link = await _linkRepo.FindByTokenHashAsync(tokenHash, ct);

        if (link == null || !link.IsActive(now))
            throw new NotFoundException();

        if (!SignaturePolicy.AllowedContentTypes.Contains(request.ContentType))
            throw new BusinessRuleException(
                "Tipo de imagen no permitido.",
                new Dictionary<string, string[]> { ["contentType"] = ["Se acepta PNG o JPEG."] });

        if (request.Size <= 0 || request.Size > SignaturePolicy.MaxSizeBytes)
            throw new BusinessRuleException(
                "La imagen supera el tamaño máximo.",
                new Dictionary<string, string[]> { ["size"] = ["Máximo 500 KB."] });

        var fileKey = SignaturePolicy.NewFileKey();
        var uploadUrl = _storage.CreatePresignedPutUrl(fileKey, request.ContentType, PutExpiry);

        return new SignatureUploadUrlResponse(uploadUrl, fileKey);
    }
}

public sealed record SignatureUploadUrlRequest(string ContentType, long Size);
public sealed record SignatureUploadUrlResponse(string UploadUrl, string FileKey);
