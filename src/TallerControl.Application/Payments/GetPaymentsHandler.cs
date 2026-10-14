using TallerControl.Application.Common;
using TallerControl.Application.Work;

namespace TallerControl.Application.Payments;

/// <summary>
/// Caso de uso: listar pagos de una OT con totales (doc 05 §2.9, T-PAY-01, REQ-15).
/// </summary>
public sealed class GetPaymentsHandler
{
    private readonly IPaymentRepository _repo;
    private readonly IWorkOrderRepository _woRepo;

    public GetPaymentsHandler(IPaymentRepository repo, IWorkOrderRepository woRepo)
    {
        _repo = repo;
        _woRepo = woRepo;
    }

    public async Task<WorkOrderPaymentsDto> HandleAsync(Guid workOrderId, CancellationToken ct)
    {
        _ = await _woRepo.FindByIdAsync(workOrderId, ct)
            ?? throw new NotFoundException("La orden no existe.");

        return await _repo.GetByWorkOrderAsync(workOrderId, ct)
            ?? new WorkOrderPaymentsDto([], 0m, 0m, 0m);
    }
}
