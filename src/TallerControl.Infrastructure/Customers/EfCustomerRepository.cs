using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Common;
using TallerControl.Application.Customers;
using TallerControl.Domain.Customers;
using TallerControl.Domain.Vehicles;
using TallerControl.Domain.Work;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Customers;

internal sealed class EfCustomerRepository : ICustomerRepository
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public EfCustomerRepository(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Page<CustomerListDto>> SearchAsync(string? q, int page, int pageSize, CancellationToken ct)
    {
        var query = _db.Set<Customer>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(c =>
                EF.Functions.ILike(c.Name, $"%{term}%") ||
                (c.DocNumber != null && EF.Functions.ILike(c.DocNumber, $"%{term}%")) ||
                (c.Phone != null && EF.Functions.ILike(c.Phone, $"%{term}%")));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CustomerListDto(
                c.Id,
                c.Name,
                c.Phone,
                c.DocNumber,
                0m)) // Balance calculado desde órdenes en E4
            .ToListAsync(ct);

        return new Page<CustomerListDto>(items, page, pageSize, total);
    }

    public async Task<CustomerDto?> GetFullAsync(Guid id, CancellationToken ct)
    {
        // IgnoreQueryFilters para aplicar la condición de taller manualmente; se excluyen
        // los clientes borrados (DeletedAt != null) salvo en FindAnyByIdAsync, que los necesita
        // para la restauración (T-CUS-06: GET de cliente borrado debe devolver 404).
        var customer = await _db.Set<Customer>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == _tenantContext.TenantId && c.DeletedAt == null, ct);

        if (customer is null) return null;

        var vehicles = await _db.Set<Vehicle>()
            .AsNoTracking()
            .Where(v => v.CustomerId == id)
            .Select(v => new VehicleBasicDto(v.Id, v.Plate, v.Brand, v.Model))
            .ToListAsync(ct);

        return new CustomerDto(
            customer.Id,
            customer.Name,
            customer.Email,
            customer.Phone,
            customer.DocType,
            customer.DocNumber,
            customer.VatCondition,
            customer.Address,
            customer.DoNotContact,
            customer.Notes,
            Balance: 0m, // E4
            vehicles,
            customer.Version);
    }

    public Task<Customer?> FindActiveByIdAsync(Guid id, CancellationToken ct) =>
        _db.Set<Customer>().FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Customer?> FindAnyByIdAsync(Guid id, CancellationToken ct) =>
        _db.Set<Customer>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == _tenantContext.TenantId, ct);

    public Task<bool> DocNumberExistsAsync(Guid? excludeId, DocType docType, string docNumber, CancellationToken ct) =>
        _db.Set<Customer>()
            .AnyAsync(c => c.DocType == docType
                && c.DocNumber == docNumber
                && (excludeId == null || c.Id != excludeId.Value), ct);

    public Task<decimal> GetPendingBalanceAsync(Guid customerId, CancellationToken ct) =>
        // Balance = suma de (total - paid_amount) de OTs no canceladas del cliente (V2: definición única).
        _db.Set<WorkOrder>()
            .Where(w => w.CustomerId == customerId && w.Status != WorkOrderStatus.Cancelled)
            .SumAsync(w => w.Total - w.PaidAmount, ct);

    public async Task AddAsync(Customer customer, CancellationToken ct) =>
        await _db.Set<Customer>().AddAsync(customer, ct);

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
