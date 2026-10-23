using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Common;
using TallerControl.Application.Work;
using TallerControl.Domain.Customers;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Users;
using TallerControl.Domain.Vehicles;
using TallerControl.Domain.Work;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Work;

internal sealed class EfWorkOrderRepository : IWorkOrderRepository
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public EfWorkOrderRepository(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Page<WorkOrderListDto>> SearchAsync(
        WorkOrderStatus? status,
        Guid? mechanicId,
        Guid? vehicleId,
        bool? withBalance,
        DateTimeOffset? from,
        DateTimeOffset? to,
        bool mechanicFilterForced,
        Guid? forcedMechanicId,
        AssignmentFilter assignment,
        int page,
        int pageSize,
        bool includePrices,
        SortSpec sort,
        CancellationToken ct,
        Guid? customerId = null)
    {
        var query = _db.Set<WorkOrder>().AsNoTracking();

        if (status.HasValue)
            query = query.Where(w => w.Status == status.Value);

        if (mechanicFilterForced && forcedMechanicId.HasValue)
            query = query.Where(w => w.AssignedMechanicId == forcedMechanicId || w.AssignedMechanicId == null);
        else if (mechanicId.HasValue)
            query = query.Where(w => w.AssignedMechanicId == mechanicId.Value);

        // Pestañas del portal del mecánico: "Mías" (asignadas a la sesión) y "Sin asignar".
        if (assignment == AssignmentFilter.Unassigned)
        {
            // Una orden cerrada o anulada no se puede tomar: no tiene sentido ofrecerla al mecánico.
            query = query.Where(w => w.AssignedMechanicId == null
                && w.Status != WorkOrderStatus.Cancelled
                && w.Status != WorkOrderStatus.Delivered
                && w.Status != WorkOrderStatus.Completed);
        }
        else if (assignment == AssignmentFilter.Mine && forcedMechanicId.HasValue)
            query = query.Where(w => w.AssignedMechanicId == forcedMechanicId);

        if (vehicleId.HasValue)
            query = query.Where(w => w.VehicleId == vehicleId.Value);

        if (customerId.HasValue)
            query = query.Where(w => w.CustomerId == customerId.Value);

        if (from.HasValue)
            query = query.Where(w => w.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(w => w.CreatedAt <= to.Value);

        if (withBalance == true)
            query = query.Where(w => w.Status != WorkOrderStatus.Cancelled && w.Total > w.PaidAmount);

        var total = await query.CountAsync(ct);

        // Claves permitidas: lista cerrada (nunca SQL con texto del cliente). El desempate por Id vuelve estable la paginación.
        var ordered = sort.Key switch
        {
            "number" => sort.Descending ? query.OrderByDescending(w => w.Number) : query.OrderBy(w => w.Number),
            "status" => sort.Descending ? query.OrderByDescending(w => w.Status) : query.OrderBy(w => w.Status),
            "total" => sort.Descending ? query.OrderByDescending(w => w.Total) : query.OrderBy(w => w.Total),
            "balance" => sort.Descending
                ? query.OrderByDescending(w => EF.Property<decimal>(w, "Balance"))
                : query.OrderBy(w => EF.Property<decimal>(w, "Balance")),
            "createdat" => sort.Descending ? query.OrderByDescending(w => w.CreatedAt) : query.OrderBy(w => w.CreatedAt),
            _ => query.OrderByDescending(w => w.CreatedAt),
        };

        var raw = await ordered
            .ThenBy(w => w.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(w => new
            {
                w.Id,
                w.Number,
                w.Status,
                w.Priority,
                w.CustomerId,
                w.VehicleId,
                w.AssignedMechanicId,
                w.Total,
                w.PaidAmount,
                Balance = EF.Property<decimal>(w, "Balance"),
                w.CreatedAt
            })
            .ToListAsync(ct);

        var customerIds = raw.Select(r => r.CustomerId).Distinct().ToList();
        var membershipIds = raw
            .Where(r => r.AssignedMechanicId.HasValue)
            .Select(r => r.AssignedMechanicId!.Value).Distinct().ToList();
        var vehicleIds = raw.Select(r => r.VehicleId).Distinct().ToList();

        var customers = await _db.Set<Customer>().AsNoTracking()
            .IgnoreQueryFilters()
            .Where(c => customerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var mechanics = membershipIds.Count > 0
            ? await _db.Set<Membership>().AsNoTracking()
                .Where(m => membershipIds.Contains(m.Id))
                .Join(_db.Set<User>().AsNoTracking(), m => m.UserId, u => u.Id,
                    (m, u) => new { m.Id, u.DisplayName })
                .ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct)
            : new Dictionary<Guid, string>();

        var vehicles = await _db.Set<Vehicle>().AsNoTracking()
            .IgnoreQueryFilters()
            .Where(v => vehicleIds.Contains(v.Id))
            .Select(v => new { v.Id, v.Plate, v.Brand, v.Model })
            .ToDictionaryAsync(v => v.Id, ct);

        var items = raw.Select(r =>
        {
            customers.TryGetValue(r.CustomerId, out var customerName);
            mechanics.TryGetValue(r.AssignedMechanicId ?? Guid.Empty, out var mechanicName);
            vehicles.TryGetValue(r.VehicleId, out var vehicle);

            return new WorkOrderListDto(
                r.Id,
                r.Number,
                r.Status,
                r.Priority,
                r.CustomerId,
                customerName ?? string.Empty,
                r.VehicleId,
                vehicle?.Plate ?? string.Empty,
                vehicle?.Brand,
                vehicle?.Model,
                r.AssignedMechanicId,
                r.AssignedMechanicId.HasValue ? mechanicName : null,
                includePrices ? r.Total : null,
                includePrices ? r.Balance : null,
                r.CreatedAt);
        }).ToList();

        return new Page<WorkOrderListDto>(items, page, pageSize, total);
    }

    public async Task<WorkOrderDto?> GetFullAsync(Guid id, bool includePrices, CancellationToken ct)
    {
        var order = await _db.Set<WorkOrder>()
            .Include(w => w.Items)
            .FirstOrDefaultAsync(w => w.Id == id, ct);

        if (order is null) return null;

        var customerName = await _db.Set<Customer>().AsNoTracking()
            .IgnoreQueryFilters()
            .Where(c => c.Id == order.CustomerId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        var vehicle = await _db.Set<Vehicle>().AsNoTracking()
            .IgnoreQueryFilters()
            .Where(v => v.Id == order.VehicleId)
            .Select(v => new { v.Plate, v.Brand, v.Model })
            .FirstOrDefaultAsync(ct);

        string? mechanicName = null;
        if (order.AssignedMechanicId.HasValue)
        {
            mechanicName = await _db.Set<Membership>().AsNoTracking()
                .Where(m => m.Id == order.AssignedMechanicId.Value)
                .Join(_db.Set<User>().AsNoTracking(), m => m.UserId, u => u.Id,
                    (_, u) => u.DisplayName)
                .FirstOrDefaultAsync(ct);
        }

        var balance = await _db.Database
            .SqlQuery<decimal>($"SELECT balance AS \"Value\" FROM work_orders WHERE id = {id}")
            .FirstOrDefaultAsync(ct);

        var itemDtos = order.Items
            .Select(i => new WorkOrderItemDto(
                i.Id, i.Kind, i.Description, i.CatalogItemId,
                i.Quantity, i.UnitNetPrice, i.VatRate,
                i.LineNet, i.LineVat, i.LineTotal, i.Position))
            .OrderBy(i => i.Position)
            .ToList();

        return new WorkOrderDto(
            order.Id, order.Number, order.Status, order.Priority,
            order.CustomerId, customerName,
            order.VehicleId, vehicle?.Plate ?? string.Empty,
            vehicle?.Brand, vehicle?.Model,
            order.AssignedMechanicId, mechanicName,
            order.OdometerKm, order.FuelLevel,
            order.ReportedIssue, order.MechanicNotes, order.InternalNotes,
            order.SourceQuoteId,
            includePrices ? order.SubtotalNet : null,
            includePrices ? order.DiscountAmount : null,
            includePrices ? order.VatAmount : null,
            includePrices ? order.Total : null,
            includePrices ? order.PaidAmount : null,
            includePrices ? balance : null,
            order.CompletedAt, order.DeliveredAt, order.CancelledAt, order.CancelReason,
            itemDtos,
            order.Version,
            order.CreatedAt);
    }

    public Task<WorkOrder?> FindByIdAsync(Guid id, CancellationToken ct) =>
        _db.Set<WorkOrder>()
            .Include(w => w.Items)
            .Include(w => w.StatusChanges)
            .FirstOrDefaultAsync(w => w.Id == id, ct);

    public Task<WorkOrder?> FindVisibleToMechanicAsync(Guid id, Guid mechanicMembershipId, CancellationToken ct) =>
        _db.Set<WorkOrder>()
            .FirstOrDefaultAsync(w => w.Id == id
                && (w.AssignedMechanicId == mechanicMembershipId || w.AssignedMechanicId == null), ct);

    public async Task<IReadOnlyList<StatusChangeDto>> GetHistoryAsync(Guid workOrderId, CancellationToken ct)
    {
        return await _db.Set<WorkOrderStatusChange>()
            .AsNoTracking()
            .Where(sc => sc.WorkOrderId == workOrderId)
            .OrderBy(sc => sc.ChangedAt)
            .Select(sc => new StatusChangeDto(
                sc.Id,
                sc.FromStatus,
                sc.ToStatus,
                sc.Note,
                sc.ChangedBy,
                null,
                sc.ChangedAt))
            .ToListAsync(ct);
    }

    public Task<Guid?> GetMainBranchIdAsync(Guid tenantId, CancellationToken ct) =>
        _db.Set<Branch>()
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.IsMain)
            .Select(b => (Guid?)b.Id)
            .FirstOrDefaultAsync(ct);

    public async Task AddAsync(WorkOrder order, CancellationToken ct) =>
        await _db.Set<WorkOrder>().AddAsync(order, ct);

    public void DeleteItem(WorkOrderItem item) => _db.Set<WorkOrderItem>().Remove(item);

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);

    public async Task<IWorkOrderTransaction> BeginTransactionAsync(CancellationToken ct)
    {
        var tx = await _db.Database.BeginTransactionAsync(ct);
        return new EfWorkOrderTransaction(tx);
    }

    private sealed class EfWorkOrderTransaction : IWorkOrderTransaction
    {
        private readonly Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction _tx;

        public EfWorkOrderTransaction(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx) => _tx = tx;

        public Task CommitAsync(CancellationToken ct) => _tx.CommitAsync(ct);

        public ValueTask DisposeAsync() => _tx.DisposeAsync();
    }
}
