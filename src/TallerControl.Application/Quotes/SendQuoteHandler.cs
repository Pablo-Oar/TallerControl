using TallerControl.Application.Common;
using TallerControl.Application.PublicLinks;
using TallerControl.Domain.PublicLinks;

namespace TallerControl.Application.Quotes;

/// <summary>
/// Caso de uso: enviar un presupuesto al cliente (Draft → Sent, doc 05 §2.6, REQ-10, T-QUO-05).
/// Genera un enlace público con el token de acceso.
/// </summary>
public sealed class SendQuoteHandler
{
    private readonly IQuoteRepository _repo;
    private readonly IPublicLinkRepository _linkRepo;
    private readonly TimeProvider _clock;

    public SendQuoteHandler(
        IQuoteRepository repo,
        IPublicLinkRepository linkRepo,
        TimeProvider clock)
    {
        _repo = repo;
        _linkRepo = linkRepo;
        _clock = clock;
    }

    public async Task<QuoteDto> HandleAsync(Guid id, CancellationToken ct)
    {
        var quote = await _repo.FindByIdAsync(id, ct)
            ?? throw new NotFoundException("El presupuesto no existe.");

        var now = _clock.GetUtcNow();

        try
        {
            quote.Send(now);
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        // Crear/renovar el enlace público — el anterior sigue activo (no se revoca).
        // El token son 32 bytes aleatorios; solo el SHA-256 va a la base.
        var tokenBytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var tokenHash = System.Security.Cryptography.SHA256.HashData(tokenBytes);

        // Vence 30 días después de la vigencia del presupuesto (doc 04 §3.6).
        var expiresAt = new DateTimeOffset(
            quote.ValidUntil.AddDays(30).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var link = new PublicLink(
            quote.TenantId,
            PublicLinkTargetType.Quote,
            quote.Id,
            tokenHash,
            expiresAt);

        await _linkRepo.AddAsync(link, ct);
        await _repo.SaveChangesAsync(ct);

        return await _repo.GetFullAsync(id, ct)
            ?? throw new InvalidOperationException("El presupuesto no se pudo leer tras el envío.");
    }
}
