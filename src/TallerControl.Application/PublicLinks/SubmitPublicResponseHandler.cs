using System.Net;
using System.Security.Cryptography;
using TallerControl.Application.Common;
using TallerControl.Application.Quotes;
using TallerControl.Domain.Quotes;

namespace TallerControl.Application.PublicLinks;

/// <summary>
/// Caso de uso: el cliente envía su respuesta (aprobación / rechazo) por el enlace público
/// (doc 05 §2.8, REQ-10, T-PUB-02..06).
/// </summary>
public sealed class SubmitPublicResponseHandler
{
    private readonly IPublicLinkRepository _linkRepo;
    private readonly IQuoteRepository _quoteRepo;
    private readonly TimeProvider _clock;

    public SubmitPublicResponseHandler(
        IPublicLinkRepository linkRepo,
        IQuoteRepository quoteRepo,
        TimeProvider clock)
    {
        _linkRepo = linkRepo;
        _quoteRepo = quoteRepo;
        _clock = clock;
    }

    public async Task<SubmitPublicResponseResult> HandleAsync(
        string tokenBase64Url,
        SubmitPublicResponseRequest request,
        string? clientIp,
        string? userAgent,
        CancellationToken ct)
    {
        var tokenHash = GetPublicQuoteHandler.HashToken(tokenBase64Url);

        var now = _clock.GetUtcNow();
        var link = await _linkRepo.FindByTokenHashAsync(tokenHash, ct);

        if (link == null || !link.IsActive(now))
            throw new NotFoundException();

        // Cargar el presupuesto (con ítems y respuestas).
        var quote = await _quoteRepo.FindByIdAsync(link.TargetId, ct)
            ?? throw new NotFoundException();

        if (quote.Status is not (QuoteStatus.Sent or QuoteStatus.Approved or QuoteStatus.PartiallyApproved))
            throw new BusinessRuleException("El presupuesto ya no acepta respuestas.");

        // Verificar el hash del contenido (T-PUB-06): evita que el cliente apruebe una versión diferente.
        var quoteDto = await _quoteRepo.GetFullAsync(quote.Id, ct)
            ?? throw new NotFoundException();

        var expectedHash = GetPublicQuoteHandler.ComputeContentHash(quoteDto);
        byte[] submittedHash;
        try
        {
            submittedHash = Convert.FromBase64String(request.ContentHash);
        }
        catch
        {
            throw new BusinessRuleException("El hash del contenido no es válido.");
        }

        if (!CryptographicOperations.FixedTimeEquals(expectedHash, submittedHash))
            throw new BusinessRuleException(
                "El presupuesto fue modificado desde que lo visualizaste. Por favor recargá la página.",
                new Dictionary<string, string[]> { ["contentHash"] = ["El contenido del presupuesto cambió."] });

        // Validar la decisión.
        if (request.Decision is not (Quote.DecisionApproved or Quote.DecisionRejected))
            throw new BusinessRuleException($"Decisión '{request.Decision}' no es válida.");

        // Construir los ítems de respuesta.
        List<QuoteResponseItem> responseItems;
        if (request.Decision == Quote.DecisionRejected)
        {
            responseItems = quote.Items
                .Select(i => new QuoteResponseItem(Guid.Empty, i.Id, approved: false))
                .ToList();
        }
        else if (request.Items != null && request.Items.Count > 0)
        {
            var itemIds = quote.Items.Select(i => i.Id).ToHashSet();
            responseItems = request.Items
                .Where(ri => itemIds.Contains(ri.QuoteItemId))
                .Select(ri => new QuoteResponseItem(Guid.Empty, ri.QuoteItemId, ri.Approved))
                .ToList();
        }
        else
        {
            // Sin ítems explícitos: aprobar todos.
            responseItems = quote.Items
                .Select(i => new QuoteResponseItem(Guid.Empty, i.Id, approved: true))
                .ToList();
        }

        // Calcular ApprovedTotal con la misma fórmula que Quote.RecalculateTotals().
        var approvedIds = responseItems.Where(ri => ri.Approved).Select(ri => ri.QuoteItemId).ToHashSet();
        var approvedItems = quote.Items.Where(i => approvedIds.Contains(i.Id)).ToList();
        var approvedSubtotal = approvedItems.Sum(i => i.LineNet);
        var approvedDiscount = decimal.Round(approvedSubtotal * quote.GlobalDiscountPct / 100m, 2, MidpointRounding.AwayFromZero);
        var approvedVat = approvedItems.Sum(i =>
            decimal.Round(i.LineNet * (1m - quote.GlobalDiscountPct / 100m) * i.VatRate / 100m, 2, MidpointRounding.AwayFromZero));
        var approvedTotal = approvedSubtotal - approvedDiscount + approvedVat;

        var clientIpParsed = ParseAndTruncateIp(clientIp);

        var response = new QuoteResponse(
            quote.TenantId,
            quote.Id,
            request.Decision,
            request.SignerName,
            approvedTotal,
            expectedHash,
            clientIpParsed,
            userAgent,
            request.Comment,
            now,
            responseItems);

        quote.RecordResponse(response);

        link.RecordAccess(now);
        await _quoteRepo.SaveChangesAsync(ct);
        await _linkRepo.SaveChangesAsync(ct);

        return new SubmitPublicResponseResult(
            request.Decision,
            approvedTotal,
            quote.Status);
    }

    private static IPAddress? ParseAndTruncateIp(string? clientIp)
    {
        if (string.IsNullOrWhiteSpace(clientIp)) return null;

        if (!IPAddress.TryParse(clientIp, out var ip)) return null;

        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            // IPv4: ceros en el último octeto → /24
            var bytes = ip.GetAddressBytes();
            bytes[3] = 0;
            return new IPAddress(bytes);
        }

        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            // IPv6: ceros en los últimos 10 bytes → /48
            var bytes = ip.GetAddressBytes();
            for (int i = 6; i < 16; i++) bytes[i] = 0;
            return new IPAddress(bytes);
        }

        return null;
    }
}
