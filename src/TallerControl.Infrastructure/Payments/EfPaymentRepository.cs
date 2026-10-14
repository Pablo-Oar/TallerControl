using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Payments;
using TallerControl.Domain.Payments;
using TallerControl.Domain.Work;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Payments;

internal sealed class EfPaymentRepository : IPaymentRepository
{
    private readonly AppDbContext _db;

    public EfPaymentRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<WorkOrderPaymentsDto?> GetByWorkOrderAsync(Guid workOrderId, CancellationToken ct)
    {
        var order = await _db.Set<WorkOrder>()
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == workOrderId, ct);

        if (order is null) return null;

        var payments = await _db.Set<Payment>()
            .AsNoTracking()
            .Where(p => p.WorkOrderId == workOrderId)
            .OrderBy(p => p.PaidAt)
            .ToListAsync(ct);

        var dtos = payments.Select(p => new PaymentDto(
            p.Id, p.Number, p.Method, p.Amount, p.PaidAt, p.Reference, p.Status,
            p.VoidReason, p.VoidedAt, string.Empty)).ToList();

        // paid_amount ya está sincronizado en la OT por AplicarPago/AnularPago.
        var paid = order.PaidAmount;
        var balance = order.Status == WorkOrderStatus.Cancelled ? 0m : order.Total - paid;

        return new WorkOrderPaymentsDto(dtos, order.Total, paid, balance);
    }

    public async Task<Payment?> FindByIdAsync(Guid id, CancellationToken ct) =>
        await _db.Set<Payment>().FirstOrDefaultAsync(p => p.Id == id, ct);


    // IgnoreQueryFilters: la búsqueda por token es pública (anónima); el tenant_id no está en sesión.
    public async Task<Payment?> FindByTokenHashAsync(byte[] tokenHash, CancellationToken ct) =>
        await _db.Set<Payment>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.PublicTokenHash == tokenHash, ct);

    public async Task<Payment?> FindByIdempotencyKeyAsync(Guid tenantId, string idempotencyKey, CancellationToken ct) =>
        await _db.Set<Payment>()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.IdempotencyKey == idempotencyKey, ct);

    public async Task AddAsync(Payment payment, CancellationToken ct) =>
        await _db.Set<Payment>().AddAsync(payment, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        _db.SaveChangesAsync(ct);
}
