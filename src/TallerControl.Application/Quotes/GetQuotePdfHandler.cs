using TallerControl.Application.Common;

namespace TallerControl.Application.Quotes;

/// <summary>Caso de uso: generar el PDF de un presupuesto (doc 05 §2.6, T-DOC-01, REQ-16).</summary>
public sealed class GetQuotePdfHandler
{
    private readonly IQuoteRepository _repo;
    private readonly IQuotePdfGenerator _pdfGenerator;

    public GetQuotePdfHandler(IQuoteRepository repo, IQuotePdfGenerator pdfGenerator)
    {
        _repo = repo;
        _pdfGenerator = pdfGenerator;
    }

    public async Task<(byte[] Bytes, int Number)> HandleAsync(Guid id, CancellationToken ct)
    {
        var quote = await _repo.GetFullAsync(id, ct)
            ?? throw new NotFoundException("El presupuesto no existe.");

        return (await _pdfGenerator.GenerateAsync(quote, ct), quote.Number);
    }
}

/// <summary>
/// Interfaz para generar el PDF de un presupuesto. Implementada en Infrastructure (QuestPDF).
/// </summary>
public interface IQuotePdfGenerator
{
    Task<byte[]> GenerateAsync(QuoteDto quote, CancellationToken ct);
}
