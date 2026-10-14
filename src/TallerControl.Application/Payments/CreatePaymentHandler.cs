using System.Security.Cryptography;
using TallerControl.Application.Common;
using TallerControl.Application.Tenancy;
using TallerControl.Application.Work;
using TallerControl.Domain.Payments;
using TallerControl.Domain.Tenancy;

namespace TallerControl.Application.Payments;

/// <summary>
/// Caso de uso: registrar un cobro sobre una OT (doc 05 §2.9, T-PAY-02..05, REQ-15).
/// Idempotente: la misma Idempotency-Key devuelve el pago original si ya existe.
/// </summary>
public sealed class CreatePaymentHandler
{
    private readonly IWorkOrderRepository _woRepo;
    private readonly IPaymentRepository _paymentRepo;
    private readonly ITenantContext _tenantContext;
    private readonly IActorContext _actorContext;
    private readonly IDocumentNumberGenerator _numbers;
    private readonly TimeProvider _clock;

    public CreatePaymentHandler(
        IWorkOrderRepository woRepo,
        IPaymentRepository paymentRepo,
        ITenantContext tenantContext,
        IActorContext actorContext,
        IDocumentNumberGenerator numbers,
        TimeProvider clock)
    {
        _woRepo = woRepo;
        _paymentRepo = paymentRepo;
        _tenantContext = tenantContext;
        _actorContext = actorContext;
        _numbers = numbers;
        _clock = clock;
    }

    public async Task<(PaymentDto Payment, decimal Balance)> HandleAsync(
        Guid workOrderId,
        CreatePaymentRequest request,
        string? idempotencyKey,
        CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        // Idempotencia: si ya existe un pago con esta clave, devolver el mismo.
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await _paymentRepo.FindByIdempotencyKeyAsync(tenantId, idempotencyKey, ct);
            if (existing is not null)
            {
                var existingBalance = (await _paymentRepo.GetByWorkOrderAsync(workOrderId, ct))?.Balance ?? 0m;
                return (MapToDto(existing), existingBalance);
            }
        }

        var order = await _woRepo.FindByIdAsync(workOrderId, ct)
            ?? throw new NotFoundException("La orden no existe.");

        if (order.Status == Domain.Work.WorkOrderStatus.Cancelled)
            throw new ConflictException("No se puede registrar un pago en una orden cancelada.");

        var now = _clock.GetUtcNow();
        var paidAt = request.PaidAt ?? now;
        var actorId = _actorContext.ActorId;

        await using var tx = await _woRepo.BeginTransactionAsync(ct);

        int number = await _numbers.NextAsync(DocumentKind.Payment, ct);

        // Token público para /c/:token (256 bits; solo el hash en BD).
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var tokenHash = SHA256.HashData(tokenBytes);
        var publicToken = Convert.ToBase64String(tokenBytes)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var payment = new Payment(
            tenantId,
            workOrderId,
            number,
            request.Method,
            request.Amount,
            paidAt,
            request.Reference,
            tokenHash,
            idempotencyKey?.Trim(),
            actorId,
            now);

        try
        {
            order.AplicarPago(request.Amount, actorId, now);
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        await _paymentRepo.AddAsync(payment, ct);
        await _woRepo.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var balance = order.Total - order.PaidAmount;
        return (MapToDto(payment, publicToken), balance);
    }

    private static PaymentDto MapToDto(Payment p, string? publicToken = null) =>
        new(p.Id, p.Number, p.Method, p.Amount, p.PaidAt, p.Reference, p.Status,
            p.VoidReason, p.VoidedAt, publicToken ?? string.Empty);
}
