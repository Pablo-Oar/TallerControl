using TallerControl.Application.Common;

namespace TallerControl.Application.Payments;

/// <summary>
/// Caso de uso: generar PDF del comprobante de pago (doc 05 §2.9, T-DOC-03, REQ-16).
/// </summary>
public sealed class GetPaymentPdfHandler
{
    private readonly IPaymentRepository _repo;
    private readonly IPaymentPdfGenerator _pdf;

    public GetPaymentPdfHandler(IPaymentRepository repo, IPaymentPdfGenerator pdf)
    {
        _repo = repo;
        _pdf = pdf;
    }

    public async Task<byte[]> HandleAsync(Guid paymentId, CancellationToken ct)
    {
        _ = await _repo.FindByIdAsync(paymentId, ct)
            ?? throw new NotFoundException("El pago no existe.");

        return await _pdf.GenerateAsync(paymentId, ct);
    }
}
