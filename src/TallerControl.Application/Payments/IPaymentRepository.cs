using TallerControl.Domain.Payments;

namespace TallerControl.Application.Payments;

public interface IPaymentRepository
{
    Task<WorkOrderPaymentsDto?> GetByWorkOrderAsync(Guid workOrderId, CancellationToken ct);
    Task<Payment?> FindByIdAsync(Guid id, CancellationToken ct);
    Task<Payment?> FindByTokenHashAsync(byte[] tokenHash, CancellationToken ct);
    Task<Payment?> FindByIdempotencyKeyAsync(Guid tenantId, string idempotencyKey, CancellationToken ct);
    Task AddAsync(Payment payment, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
