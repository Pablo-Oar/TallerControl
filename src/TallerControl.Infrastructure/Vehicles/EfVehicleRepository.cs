using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Common;
using TallerControl.Application.Vehicles;
using TallerControl.Domain.Customers;
using TallerControl.Domain.Quotes;
using TallerControl.Domain.Vehicles;
using TallerControl.Domain.Work;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Vehicles;

internal sealed class EfVehicleRepository : IVehicleRepository
{
    private readonly AppDbContext _db;

    public EfVehicleRepository(AppDbContext db) => _db = db;

    public async Task<Page<VehicleListDto>> SearchAsync(string? q, int page, int pageSize, SortSpec sort, CancellationToken ct)
    {
        var query = _db.Set<Vehicle>()
            .AsNoTracking()
            .Join(_db.Set<Customer>().AsNoTracking(),
                v => v.CustomerId,
                c => c.Id,
                (v, c) => new { Vehicle = v, CustomerName = c.Name });

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            // La patente se normaliza antes de buscar (T-VEH-01)
            var normalizedTerm = term.ToUpperInvariant().Replace("-", "").Replace(" ", "");
            query = query.Where(x =>
                EF.Functions.ILike(x.Vehicle.Plate, $"%{normalizedTerm}%") ||
                (x.Vehicle.Brand != null && EF.Functions.ILike(x.Vehicle.Brand, $"%{term}%")) ||
                (x.Vehicle.Model != null && EF.Functions.ILike(x.Vehicle.Model, $"%{term}%")) ||
                EF.Functions.ILike(x.CustomerName, $"%{term}%"));
        }

        var total = await query.CountAsync(ct);

        var ordered = sort.Key switch
        {
            "plate" => sort.Descending ? query.OrderByDescending(x => x.Vehicle.Plate) : query.OrderBy(x => x.Vehicle.Plate),
            "brand" => sort.Descending ? query.OrderByDescending(x => x.Vehicle.Brand) : query.OrderBy(x => x.Vehicle.Brand),
            "model" => sort.Descending ? query.OrderByDescending(x => x.Vehicle.Model) : query.OrderBy(x => x.Vehicle.Model),
            "year" => sort.Descending ? query.OrderByDescending(x => x.Vehicle.Year) : query.OrderBy(x => x.Vehicle.Year),
            "customer" => sort.Descending ? query.OrderByDescending(x => x.CustomerName) : query.OrderBy(x => x.CustomerName),
            _ => query.OrderBy(x => x.Vehicle.Plate),
        };

        var items = await ordered
            .ThenBy(x => x.Vehicle.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new VehicleListDto(
                x.Vehicle.Id,
                x.Vehicle.Plate,
                x.Vehicle.Brand,
                x.Vehicle.Model,
                x.Vehicle.Year,
                x.CustomerName))
            .ToListAsync(ct);

        return new Page<VehicleListDto>(items, page, pageSize, total);
    }

    public async Task<VehicleDto?> GetFullAsync(Guid id, CancellationToken ct)
    {
        var result = await _db.Set<Vehicle>()
            .AsNoTracking()
            .Where(v => v.Id == id)
            .Join(_db.Set<Customer>().AsNoTracking(),
                v => v.CustomerId,
                c => c.Id,
                (v, c) => new { Vehicle = v, CustomerName = c.Name })
            .FirstOrDefaultAsync(ct);

        if (result is null) return null;

        var v = result.Vehicle;
        return new VehicleDto(
            v.Id,
            v.Plate,
            v.CustomerId,
            result.CustomerName,
            v.Brand,
            v.Model,
            v.VersionName,
            v.Color,
            v.Year,
            v.Vin,
            v.Fuel,
            v.OdometerKm,
            v.UsageProfile?.ToString(),
            v.EngineNumber,
            ActiveQrCard: null, // E8
            v.Version);
    }

    public async Task<Page<HistoryEntryDto>> GetHistoryAsync(
        Guid vehicleId, bool includePrices, int page, int pageSize, CancellationToken ct)
    {
        var orders = _db.Set<WorkOrder>().AsNoTracking().Where(w => w.VehicleId == vehicleId);
        var quotes = _db.Set<Quote>().AsNoTracking().Where(q => q.VehicleId == vehicleId);
        var total = await orders.CountAsync(ct) + await quotes.CountAsync(ct);

        // Cada fuente aporta como mucho las filas que caben hasta la página pedida; se mezclan en memoria.
        var take = page * pageSize;
        var woRows = await orders.OrderByDescending(w => w.CreatedAt).Take(take)
            .Select(w => new { w.Id, w.Number, w.CreatedAt, w.OdometerKm, w.Status, w.Total })
            .ToListAsync(ct);
        var quoteRows = await quotes.OrderByDescending(q => q.CreatedAt).Take(take)
            .Select(q => new { q.Id, q.Number, q.CreatedAt, q.Status, q.Total })
            .ToListAsync(ct);

        var entries = woRows
            .Select(w => new HistoryEntryDto(
                "work_order", w.Id, $"OT-{w.Number:D4}", w.CreatedAt, w.OdometerKm,
                EnumText.ToSnakeCase(w.Status.ToString()), includePrices ? w.Total : null))
            .Concat(quoteRows.Select(q => new HistoryEntryDto(
                "quote", q.Id, $"P-{q.Number:D4}", q.CreatedAt, null,
                EnumText.ToSnakeCase(q.Status.ToString()), includePrices ? q.Total : null)))
            .OrderByDescending(e => e.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new Page<HistoryEntryDto>(entries, page, pageSize, total);
    }

    public Task<Vehicle?> FindByIdAsync(Guid id, CancellationToken ct) =>
        _db.Set<Vehicle>().FirstOrDefaultAsync(v => v.Id == id, ct);

    public Task<bool> PlateExistsAsync(Guid? excludeId, string normalizedPlate, CancellationToken ct) =>
        _db.Set<Vehicle>()
            .AnyAsync(v => v.Plate == normalizedPlate
                && (excludeId == null || v.Id != excludeId.Value), ct);

    public Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken ct) =>
        _db.Set<Customer>().AnyAsync(c => c.Id == customerId, ct);

    public Task<bool> HasOpenWorkOrdersAsync(Guid vehicleId, CancellationToken ct) =>
        _db.Set<WorkOrder>()
            .AnyAsync(w => w.VehicleId == vehicleId
                && w.Status != WorkOrderStatus.Cancelled
                && w.Status != WorkOrderStatus.Delivered, ct);

    public async Task AddAsync(Vehicle vehicle, CancellationToken ct) =>
        await _db.Set<Vehicle>().AddAsync(vehicle, ct);

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
