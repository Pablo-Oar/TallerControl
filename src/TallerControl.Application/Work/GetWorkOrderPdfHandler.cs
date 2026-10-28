using TallerControl.Application.Common;

namespace TallerControl.Application.Work;

/// <summary>
/// Caso de uso: generar PDF de la OT (doc 05 §2.7, T-DOC-02, REQ-16).
/// La generación real delega a <see cref="IWorkOrderPdfGenerator"/> (Bloque 7).
/// </summary>
public sealed class GetWorkOrderPdfHandler
{
    private readonly IWorkOrderRepository _repo;
    private readonly IWorkOrderPdfGenerator _pdfGenerator;

    public GetWorkOrderPdfHandler(IWorkOrderRepository repo, IWorkOrderPdfGenerator pdfGenerator)
    {
        _repo = repo;
        _pdfGenerator = pdfGenerator;
    }

    /// <summary>
    /// El PDF sigue las mismas reglas que el detalle (REQ-14): un mecánico solo obtiene las órdenes propias o sin
    /// asignar, y los montos solo viajan a quien tiene <c>prices.view</c> (si no, el PDF sale sin importes).
    /// </summary>
    public async Task<(byte[] Bytes, int Number)> HandleAsync(
        Guid id,
        bool isMechanic,
        Guid? mechanicMembershipId,
        bool includePrices,
        CancellationToken ct)
    {
        var order = await _repo.GetFullAsync(id, includePrices, ct)
            ?? throw new NotFoundException("La orden no existe.");

        return (await _pdfGenerator.GenerateAsync(order, ct), order.Number);
    }
}

/// <summary>
/// Interfaz para generar el PDF de una OT. Implementada en Infrastructure (QuestPDF, Bloque 7).
/// </summary>
public interface IWorkOrderPdfGenerator
{
    Task<byte[]> GenerateAsync(WorkOrderDto order, CancellationToken ct);
}
