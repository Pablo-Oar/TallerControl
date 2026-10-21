using TallerControl.Application.Cash;
using TallerControl.Application.Common;
using TallerControl.Application.Work;
using TallerControl.Domain.Cash;

namespace TallerControl.Application.Payments;

/// <summary>
/// Caso de uso: anular un pago (doc 05 §2.9, T-PAY-06, REQ-15).
/// Restaura el saldo de la orden. Solo Owner puede anular (payments.void).
/// </summary>
public sealed class VoidPaymentHandler
{
    private readonly IPaymentRepository _paymentRepo;
    private readonly IWorkOrderRepository _woRepo;
    private readonly ICashRepository _cashRepo;
    private readonly ITenantContext _tenantContext;
    private readonly IActorContext _actorContext;
    private readonly TimeProvider _clock;

    public VoidPaymentHandler(
        IPaymentRepository paymentRepo,
        IWorkOrderRepository woRepo,
        ICashRepository cashRepo,
        ITenantContext tenantContext,
        IActorContext actorContext,
        TimeProvider clock)
    {
        _paymentRepo = paymentRepo;
        _woRepo = woRepo;
        _cashRepo = cashRepo;
        _tenantContext = tenantContext;
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

        // D-31: si el cobro había entrado a una cuenta de caja, la anulación deja el movimiento inverso
        // (los movimientos son de solo inserción: no se borra el original).
        if (payment.AccountId.HasValue)
        {
            var tenantId = _tenantContext.TenantId
                ?? throw new InvalidOperationException("No hay taller en la sesión.");
            var account = await _cashRepo.FindAccountByIdAsync(tenantId, payment.AccountId.Value, ct);
            if (account is not null)
            {
                await _cashRepo.AddMovementAsync(new CashMovement(
                    tenantId,
                    account.BranchId,
                    account.Id,
                    payment.Amount,
                    CashMovementDirection.Out,
                    CashMovementReason.ManualExpense,
                    referenceType: "payment_void",
                    referenceId: payment.Id,
                    note: $"Anulación del cobro #{payment.Number}",
                    createdByMembershipId: actorId,
                    createdAt: now), ct);
            }
        }

        await _paymentRepo.SaveChangesAsync(ct);

        return await _paymentRepo.GetByWorkOrderAsync(payment.WorkOrderId, ct)
            ?? throw new InvalidOperationException("No se pudieron leer los pagos actualizados.");
    }
}
