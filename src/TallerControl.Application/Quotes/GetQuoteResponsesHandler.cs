using TallerControl.Application.Common;
using TallerControl.Application.PublicLinks;

namespace TallerControl.Application.Quotes;

/// <summary>Caso de uso: obtener historial de respuestas de un presupuesto (doc 05 §2.6).</summary>
public sealed class GetQuoteResponsesHandler
{
    private static readonly TimeSpan SignatureUrlLifetime = TimeSpan.FromMinutes(10);

    private readonly IQuoteRepository _repo;
    private readonly IObjectStorage _storage;

    public GetQuoteResponsesHandler(IQuoteRepository repo, IObjectStorage storage)
    {
        _repo = repo;
        _storage = storage;
    }

    public async Task<IReadOnlyList<QuoteResponseDto>> HandleAsync(Guid quoteId, CancellationToken ct)
    {
        var quote = await _repo.GetFullAsync(quoteId, ct)
            ?? throw new NotFoundException("El presupuesto no existe.");

        _ = quote; // confirma existencia y aislamiento de taller
        var responses = await _repo.GetResponsesAsync(quoteId, ct);

        // La firma se entrega con una URL de corta vida; la clave del archivo no sale del servidor.
        return responses
            .Select(r => r.SignatureKey is null
                ? r
                : r with { SignatureUrl = _storage.CreatePresignedGetUrl(r.SignatureKey, SignatureUrlLifetime) })
            .ToList();
    }
}

/// <summary>
/// Revela el documento que declaró el cliente al firmar. Se guarda cifrado y solo lo ve quien tiene permiso de
/// auditoría (el Dueño); el acceso queda en el registro del servidor.
/// </summary>
public sealed class GetSignerDocumentHandler
{
    private readonly IQuoteRepository _repo;
    private readonly ISignatureEncryptor _encryptor;

    public GetSignerDocumentHandler(IQuoteRepository repo, ISignatureEncryptor encryptor)
    {
        _repo = repo;
        _encryptor = encryptor;
    }

    public async Task<SignerDocumentDto> HandleAsync(Guid quoteId, Guid responseId, CancellationToken ct)
    {
        _ = await _repo.GetFullAsync(quoteId, ct)
            ?? throw new NotFoundException("El presupuesto no existe.");

        var cipher = await _repo.GetSignerDocumentCipherAsync(quoteId, responseId, ct);
        if (cipher is not { Length: > 0 })
            throw new NotFoundException("La respuesta no tiene documento registrado.");

        return new SignerDocumentDto(_encryptor.Decrypt(cipher));
    }
}
