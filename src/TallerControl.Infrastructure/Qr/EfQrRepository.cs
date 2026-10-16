using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Common;
using TallerControl.Application.Qr;
using TallerControl.Domain.Customers;
using TallerControl.Domain.Qr;
using TallerControl.Domain.Quotes;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Vehicles;
using TallerControl.Domain.Work;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Qr;

internal sealed class EfQrRepository : IQrRepository
{
    private readonly AppDbContext _db;

    public EfQrRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<QrCard?> FindByIdAsync(Guid id, CancellationToken ct) =>
        _db.Set<QrCard>().FirstOrDefaultAsync(q => q.Id == id, ct);

    // IgnoreQueryFilters: la búsqueda por código es pública (sin tenant en sesión).
    public Task<QrCard?> FindByCodeAsync(string code, CancellationToken ct) =>
        _db.Set<QrCard>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(q => q.Code == code, ct);

    // IgnoreQueryFilters: usado en la validación OTP pública, sin tenant en sesión.
    public Task<QrCard?> FindByIdPublicAsync(Guid id, CancellationToken ct) =>
        _db.Set<QrCard>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(q => q.Id == id, ct);

    public Task<QrCard?> FindActiveByVehicleAsync(Guid vehicleId, CancellationToken ct) =>
        _db.Set<QrCard>()
            .FirstOrDefaultAsync(q => q.VehicleId == vehicleId
                && (q.Status == QrCardStatus.Assigned || q.Status == QrCardStatus.Lost), ct);

    public async Task<QrCardDto?> GetCardDtoAsync(Guid id, CancellationToken ct)
    {
        var card = await _db.Set<QrCard>()
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == id, ct);

        if (card is null) return null;

        string? plate = null;
        string? brand = null;
        string? model = null;

        if (card.VehicleId.HasValue)
        {
            var vehicle = await _db.Set<Vehicle>()
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Select(v => new { v.Id, v.Plate, v.Brand, v.Model })
                .FirstOrDefaultAsync(v => v.Id == card.VehicleId.Value, ct);

            plate = vehicle?.Plate;
            brand = vehicle?.Brand;
            model = vehicle?.Model;
        }

        return new QrCardDto(card.Id, card.Code, card.Status, card.VehicleId, plate, brand, model,
            card.AssignedAt, card.RevokedAt, card.RevokeReason);
    }

    public async Task<Page<QrCardListDto>> SearchAsync(
        QrCardStatus? status, string? q, int page, int pageSize, CancellationToken ct)
    {
        var query = _db.Set<QrCard>().AsNoTracking();

        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(c => c.Code.Contains(q.ToUpperInvariant()));

        var total = await query.CountAsync(ct);

        var cards = await query
            .OrderByDescending(c => c.AssignedAt ?? c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Cargar patentes de los vehículos asignados
        var vehicleIds = cards
            .Where(c => c.VehicleId.HasValue)
            .Select(c => c.VehicleId!.Value)
            .Distinct()
            .ToList();

        var plates = vehicleIds.Count > 0
            ? await _db.Set<Vehicle>().AsNoTracking().IgnoreQueryFilters()
                .Where(v => vehicleIds.Contains(v.Id))
                .Select(v => new { v.Id, v.Plate })
                .ToDictionaryAsync(v => v.Id, v => v.Plate, ct)
            : new Dictionary<Guid, string>();

        var dtos = cards.Select(c => new QrCardListDto(
            c.Id, c.Code, c.Status, c.VehicleId,
            c.VehicleId.HasValue ? plates.GetValueOrDefault(c.VehicleId.Value) : null,
            c.AssignedAt, c.RevokedAt)).ToList();

        return new Page<QrCardListDto>(dtos, page, pageSize, total);
    }

    public async Task<Page<QrEventDto>> GetEventsAsync(Guid qrCardId, int page, int pageSize, CancellationToken ct)
    {
        var query = _db.Set<QrScanEvent>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(e => e.QrCardId == qrCardId);

        var total = await query.CountAsync(ct);

        var events = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new QrEventDto(e.Id, e.EventType, e.Outcome, e.IpTruncated, e.CreatedAt))
            .ToListAsync(ct);

        return new Page<QrEventDto>(events, page, pageSize, total);
    }

    public async Task AddRangeAsync(IEnumerable<QrCard> cards, CancellationToken ct) =>
        await _db.Set<QrCard>().AddRangeAsync(cards, ct);

    public async Task AddScanEventAsync(QrScanEvent scanEvent, CancellationToken ct) =>
        await _db.Set<QrScanEvent>().AddAsync(scanEvent, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        _db.SaveChangesAsync(ct);

    // --- Métodos autenticados ---

    public async Task<QrVehicleSummaryDto?> GetVehicleSummaryByCodeAsync(string code, CancellationToken ct)
    {
        // Sin IgnoreQueryFilters: el HasQueryFilter filtra por tenant del contexto de sesión.
        var card = await _db.Set<QrCard>()
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Code == code, ct);

        if (card is null) return null;

        string? plate = null, brand = null, model = null, customerName = null;

        if (card.VehicleId.HasValue)
        {
            var vehicle = await _db.Set<Vehicle>()
                .AsNoTracking()
                .Where(v => v.Id == card.VehicleId.Value)
                .Select(v => new { v.Plate, v.Brand, v.Model, v.CustomerId })
                .FirstOrDefaultAsync(ct);

            if (vehicle is not null)
            {
                plate = vehicle.Plate;
                brand = vehicle.Brand;
                model = vehicle.Model;

                customerName = await _db.Set<Customer>()
                    .AsNoTracking()
                    .Where(c => c.Id == vehicle.CustomerId)
                    .Select(c => c.Name)
                    .FirstOrDefaultAsync(ct);
            }
        }

        return new QrVehicleSummaryDto(
            card.Id, card.Code, card.Status.ToString().ToLowerInvariant(),
            card.VehicleId, plate, brand, model, customerName);
    }

    // --- Métodos públicos (IgnoreQueryFilters) ---

    public async Task<WorkshopInfoDto?> GetWorkshopInfoByCardIdAsync(Guid qrCardId, CancellationToken ct)
    {
        var card = await _db.Set<QrCard>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(q => q.Id == qrCardId, ct);

        if (card is null) return null;

        var name = await _db.Set<Tenant>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => t.Id == card.TenantId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(ct);

        return new WorkshopInfoDto(name ?? "Taller", null);
    }

    public async Task<VehicleOwnerInfo?> GetVehicleOwnerForChallengeAsync(Guid vehicleId, CancellationToken ct)
    {
        var result = await (
            from v in _db.Set<Vehicle>().AsNoTracking().IgnoreQueryFilters()
            join c in _db.Set<Customer>().AsNoTracking().IgnoreQueryFilters()
                on v.CustomerId equals c.Id
            where v.Id == vehicleId && v.DeletedAt == null && c.DeletedAt == null
            select new
            {
                v.Plate,
                v.TenantId,
                c.Id,
                c.Email,
                c.OwnerAccessCodeHash
            }).FirstOrDefaultAsync(ct);

        if (result is null) return null;

        return new VehicleOwnerInfo(result.Plate, result.Id, result.TenantId, result.Email, result.OwnerAccessCodeHash);
    }

    public async Task<OwnerVehicleDto?> GetOwnerVehicleDtoAsync(Guid vehicleId, Guid tenantId, CancellationToken ct)
    {
        var vehicle = await _db.Set<Vehicle>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.Id == vehicleId && v.TenantId == tenantId && v.DeletedAt == null, ct);

        if (vehicle is null) return null;

        // Órdenes de trabajo del vehículo (no canceladas) — sin costos/notas internas
        var orders = await _db.Set<WorkOrder>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(w => w.VehicleId == vehicleId && w.TenantId == tenantId
                && w.Status != WorkOrderStatus.Cancelled)
            .OrderByDescending(w => w.CreatedAt)
            .Take(50)
            .Select(w => new OwnerWorkOrderDto(
                w.Number,
                w.Status.ToString().ToLowerInvariant(),
                w.CreatedAt,
                w.DeliveredAt ?? w.CompletedAt,
                w.Total))
            .ToListAsync(ct);

        // Saldo pendiente = suma de balance de órdenes no canceladas (V2: definición única de deuda)
        var pendingBalance = await _db.Set<WorkOrder>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(w => w.VehicleId == vehicleId && w.TenantId == tenantId
                && w.Status != WorkOrderStatus.Cancelled)
            .SumAsync(w => EF.Property<decimal>(w, "Balance"), ct);

        // Presupuestos activos (no cancelados, rechazados, expirados ni convertidos)
        var activeStatuses = new[] { QuoteStatus.Draft, QuoteStatus.Sent, QuoteStatus.Approved, QuoteStatus.PartiallyApproved };
        var quotes = await _db.Set<Quote>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(q => q.VehicleId == vehicleId && q.TenantId == tenantId
                && activeStatuses.Contains(q.Status))
            .OrderByDescending(q => q.CreatedAt)
            .Take(10)
            .Select(q => new OwnerQuoteDto(
                q.Number,
                q.Status.ToString().ToLowerInvariant(),
                null,  // PublicLink: no reconstruible desde hash; el dueño accede vía portal QR
                q.ValidUntil.ToDateTime(TimeOnly.MinValue)))
            .ToListAsync(ct);

        return new OwnerVehicleDto(
            vehicle.Plate, vehicle.Brand, vehicle.Model, vehicle.OdometerKm,
            orders, quotes, pendingBalance);
    }
}
