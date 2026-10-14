using TallerControl.Application.Common;
using TallerControl.Application.Work;

namespace TallerControl.Application.Payments;

/// <summary>
/// Caso de uso: anular un pago (doc 05 §2.9, T-PAY-06, REQ-15).
/// Restaura el saldo de la orden. Solo Owner puede anular (payments.void).
/// </summary>
public sealed class VoidPaymentHandler
{
    private readonly IPaymentRepository _paymentRepo;
    private readonly IWorkOrderRepository _woRepo;
    private readonly IActorContext _actorContext;
    private readonly TimeProvider _clock;

    public VoidPaymentHandler(
        IPaymentRepository paymentRepo,
        IWorkOrderRepository woRepo,
        IActorContext actorContext,
        TimeProvider clock)
    {
        _paymentRepo = paymentRepo;
        _woRepo = woRepo;
        _actorContext = actorContext;
        _clock = clock;
    }

    public async Task<WorkOrderPaymentsDto> HandleAsync(Guid paymentId, VoidPaymentRequest request, CancellationToken ct)
    {
        var payment = await _paymentRepo.FindByIdAsync(paymentId, ct)
            ?? throw new NotFoundException("El pago no existe.");

        var order = await _woRepo.FindByIdAsync(payment.WorkOrderId, ct)
            ?? throw new NotFoundException("La orden asociada no existe.");

        var actorId = _actorContext.ActorId;
        var now = _clock.GetUtcNow();

        try
        {
            payment.Anular(request.Reason, actorId, now);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        order.AnularPago(payment.Amount, actorId, now);

        await _paymentRepo.SaveChangesAsync(ct);

        return await _paymentRepo.GetByWorkOrderAsync(payment.WorkOrderId, ct)
            ?? throw new InvalidOperationException("No se pudieron leer los pagos actualizados.");
    }
}
