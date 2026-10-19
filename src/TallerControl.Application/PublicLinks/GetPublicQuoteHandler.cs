using System.Security.Cryptography;
using TallerControl.Application.Common;
using TallerControl.Application.Quotes;

namespace TallerControl.Application.PublicLinks;

/// <summary>
/// Caso de uso: obtener la vista pública de un presupuesto por token (doc 05 §2.8, T-PUB-01).
/// Responde 404 para token inválido, revocado, expirado o presupuesto no enviado.
/// </summary>
public sealed class GetPublicQuoteHandler
{
    private readonly IPublicLinkRepository _linkRepo;
    private readonly IQuoteRepository _quoteRepo;
    private readonly TimeProvider _clock;

    public GetPublicQuoteHandler(
        IPublicLinkRepository linkRepo,
        IQuoteRepository quoteRepo,
        TimeProvider clock)
    {
        _linkRepo = linkRepo;
        _quoteRepo = quoteRepo;
        _clock = clock;
    }

    public async Task<PublicQuoteDto> HandleAsync(string tokenBase64Url, CancellationToken ct)
    {
        var tokenHash = HashToken(tokenBase64Url);

        var now = _clock.GetUtcNow();
        var link = await _linkRepo.FindByTokenHashAsync(tokenHash, ct);

        if (link == null || !link.IsActive(now))
            throw new NotFoundException();

        var quote = await _quoteRepo.GetFullPublicAsync(link.TargetId, ct);

        // El presupuesto debe existir y estar en un estado visible al cliente.
        if (quote == null || quote.Status is
            Domain.Quotes.QuoteStatus.Draft or
            Domain.Quotes.QuoteStatus.Cancelled)
            throw new NotFoundException();

        link.RecordAccess(now);
        await _linkRepo.SaveChangesAsync(ct);

        return MapToPublicDto(quote);
    }

    private static PublicQuoteDto MapToPublicDto(QuoteDto q)
    {
        var contentHash = ComputeContentHash(q);

        return new PublicQuoteDto(
            q.Id, q.Number, q.Status,
            q.CustomerName, q.VehiclePlate, q.VehicleBrand, q.VehicleModel,
            q.GlobalDiscountPct, q.SubtotalNet, q.DiscountAmount, q.VatAmount, q.Total,
            q.ValidUntil, q.CustomerNotes,
            q.Items.Select(i => new PublicQuoteItemDto(
                i.Id, i.Kind, i.Description,
                i.Quantity, i.UnitNetPrice, i.VatRate,
                i.LineNet, i.LineVat, i.LineTotal,
                i.Position, i.RequiredForApproval)).ToList(),
            Convert.ToBase64String(contentHash),
            q.CreatedAt);
    }

    internal static byte[] ComputeContentHash(QuoteDto q)
    {
        // Hash determinístico del contenido facturable (sin notas internas ni timestamps).
        var content = string.Join("|",
            q.Id,
            q.GlobalDiscountPct.ToString("F4"),
            string.Join(",", q.Items.OrderBy(i => i.Position)
                .Select(i => $"{i.Id}:{i.Quantity:F3}:{i.UnitNetPrice:F2}:{i.VatRate:F2}")));

        return SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content));
    }

    internal static byte[] HashToken(string tokenBase64Url)
    {
        byte[] tokenBytes;
        try
        {
            // Soporta tanto Base64 estándar como Base64Url.
            var b64 = tokenBase64Url.Replace('-', '+').Replace('_', '/');
            switch (b64.Length % 4)
            {
                case 2: b64 += "=="; break;
                case 3: b64 += "="; break;
            }
            tokenBytes = Convert.FromBase64String(b64);
        }
        catch
        {
            throw new NotFoundException();
        }

        return SHA256.HashData(tokenBytes);
    }
}
