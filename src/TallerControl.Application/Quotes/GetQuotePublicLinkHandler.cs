using System.Security.Cryptography;
using TallerControl.Application.Common;
using TallerControl.Application.PublicLinks;
using TallerControl.Application.Tenancy;
using TallerControl.Domain.PublicLinks;
using TallerControl.Domain.Quotes;

namespace TallerControl.Application.Quotes;

/// <summary>
/// Caso de uso: obtener el enlace público de un presupuesto enviado para compartirlo con el cliente
/// (doc 05 §2.6 "enviar y obtener link", REQ-10). Como en la OT, el token solo existe en texto plano en esta
/// respuesta (la base guarda su SHA-256), así que cada llamada revoca los enlaces activos y emite uno nuevo.
/// </summary>
public sealed class GetQuotePublicLinkHandler
{
    private readonly IQuoteRepository _quotes;
    private readonly IPublicLinkRepository _links;
    private readonly ITenantContext _tenantContext;
    private readonly IActorContext _actorContext;
    private readonly TimeProvider _clock;

    public GetQuotePublicLinkHandler(
        IQuoteRepository quotes,
        IPublicLinkRepository links,
        ITenantContext tenantContext,
        IActorContext actorContext,
        TimeProvider clock)
    {
        _quotes = quotes;
        _links = links;
        _tenantContext = tenantContext;
        _actorContext = actorContext;
        _clock = clock;
    }

    public async Task<QuotePublicLinkDto> HandleAsync(Guid quoteId, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        var quote = await _quotes.FindByIdAsync(quoteId, ct)
            ?? throw new NotFoundException("El presupuesto no existe.");

        // Un borrador no se comparte (hay que enviarlo); uno cancelado o convertido ya no admite respuestas.
        if (quote.Status is not (QuoteStatus.Sent or QuoteStatus.Approved or QuoteStatus.PartiallyApproved
            or QuoteStatus.Rejected or QuoteStatus.Expired))
            throw new BusinessRuleException("El presupuesto debe estar enviado para obtener su enlace.");

        var now = _clock.GetUtcNow();

        foreach (var link in await _links.FindActiveByTargetAsync(PublicLinkTargetType.Quote, quoteId, now, ct))
            link.Revoke(_actorContext.ActorId, now);

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var tokenHash = SHA256.HashData(tokenBytes);

        // Mismo criterio que SendQuoteHandler: vence 30 días después de la vigencia del presupuesto.
        var expiresAt = new DateTimeOffset(quote.ValidUntil.AddDays(30).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        if (expiresAt <= now) expiresAt = now.AddDays(30);

        await _links.AddAsync(new PublicLink(tenantId, PublicLinkTargetType.Quote, quoteId, tokenHash, expiresAt), ct);
        await _links.SaveChangesAsync(ct);

        var token = Convert.ToBase64String(tokenBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return new QuotePublicLinkDto(token, expiresAt);
    }
}

/// <summary>Respuesta de <c>POST /api/v1/quotes/{id}/public-link</c>: el token va en la URL <c>/p/{token}</c>.</summary>
public sealed record QuotePublicLinkDto(string TokenBase64Url, DateTimeOffset ExpiresAt);
