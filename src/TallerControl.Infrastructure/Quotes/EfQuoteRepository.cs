using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Common;
using TallerControl.Application.Quotes;
using TallerControl.Domain.Customers;
using TallerControl.Domain.Quotes;
using TallerControl.Domain.Vehicles;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Quotes;

internal sealed class EfQuoteRepository : IQuoteRepository
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public EfQuoteRepository(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Page<QuoteListDto>> SearchAsync(
        QuoteStatus? status,
        Guid? customerId,
        Guid? vehicleId,
        DateOnly? from,
        DateOnly? to,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var query = _db.Set<Quote>().AsNoTracking();

        if (status.HasValue)
            query = query.Where(q => q.Status == status.Value);
        if (customerId.HasValue)
            query = query.Where(q => q.CustomerId == customerId.Value);
        if (vehicleId.HasValue)
            query = query.Where(q => q.VehicleId == vehicleId.Value);
        if (from.HasValue)
            query = query.Where(q => q.ValidUntil >= from.Value);
        if (to.HasValue)
            query = query.Where(q => q.ValidUntil <= to.Value);

        var total = await query.CountAsync(ct);

        var raw = await query
            .OrderByDescending(q => q.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(q => new
            {
                q.Id,
                q.Number,
                q.Status,
                q.CustomerId,
                q.VehicleId,
                q.Total,
                q.GlobalDiscountPct,
                q.ValidUntil,
                q.CreatedAt
            })
            .ToListAsync(ct);

        var customerIds = raw.Select(r => r.CustomerId).Distinct().ToList();
        var vehicleIds = raw.Select(r => r.VehicleId).Distinct().ToList();

        var customers = await _db.Set<Customer>().AsNoTracking()
            .IgnoreQueryFilters()
            .Where(c => customerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var vehicles = await _db.Set<Vehicle>().AsNoTracking()
            .IgnoreQueryFilters()
            .Where(v => vehicleIds.Contains(v.Id))
            .Select(v => new { v.Id, v.Plate })
            .ToDictionaryAsync(v => v.Id, ct);

        var items = raw.Select(r =>
        {
            customers.TryGetValue(r.CustomerId, out var customerName);
            vehicles.TryGetValue(r.VehicleId, out var vehicle);

            return new QuoteListDto(
                r.Id, r.Number, r.Status,
                r.CustomerId, customerName ?? string.Empty,
                r.VehicleId, vehicle?.Plate ?? string.Empty,
                r.Total, r.GlobalDiscountPct, r.ValidUntil, r.CreatedAt);
        }).ToList();

        return new Page<QuoteListDto>(items, page, pageSize, total);
    }

    public async Task<QuoteDto?> GetFullAsync(Guid id, CancellationToken ct)
    {
        var quote = await _db.Set<Quote>()
            .Include(q => q.Items)
            .FirstOrDefaultAsync(q => q.Id == id, ct);

        if (quote is null) return null;

        var customerName = await _db.Set<Customer>().AsNoTracking()
            .IgnoreQueryFilters()
            .Where(c => c.Id == quote.CustomerId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        var vehicle = await _db.Set<Vehicle>().AsNoTracking()
            .IgnoreQueryFilters()
            .Where(v => v.Id == quote.VehicleId)
            .Select(v => new { v.Plate, v.Brand, v.Model })
            .FirstOrDefaultAsync(ct);

        var itemDtos = quote.Items
            .Select(i => new QuoteItemDto(
                i.Id, i.Kind, i.Description, i.CatalogItemId,
                i.Quantity, i.UnitNetPrice, i.VatRate,
                i.LineNet, i.LineVat, i.LineTotal, i.Position, i.RequiredForApproval))
            .OrderBy(i => i.Position)
            .ToList();

        return new QuoteDto(
            quote.Id, quote.Number, quote.Status,
            quote.CustomerId, customerName,
            quote.VehicleId, vehicle?.Plate ?? string.Empty,
            vehicle?.Brand, vehicle?.Model,
            quote.GlobalDiscountPct,
            quote.SubtotalNet, quote.DiscountAmount, quote.VatAmount, quote.Total,
            quote.ApprovedTotal, quote.ValidUntil,
            quote.CustomerNotes, quote.InternalNotes,
            quote.WorkOrderId, quote.SentAt, quote.ConvertedAt, quote.CancelReason,
            itemDtos,
            quote.Version,
            quote.CreatedAt);
    }

    public Task<Quote?> FindByIdAsync(Guid id, CancellationToken ct) =>
        _db.Set<Quote>()
            .Include(q => q.Items)
            .Include(q => q.Responses).ThenInclude(r => r.Items)
            .FirstOrDefaultAsync(q => q.Id == id, ct);

    public async Task<IReadOnlyList<QuoteResponseDto>> GetResponsesAsync(Guid quoteId, CancellationToken ct)
    {
        var responses = await _db.Set<QuoteResponse>()
            .AsNoTracking()
            .Include(r => r.Items)
            .Where(r => r.QuoteId == quoteId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return responses.Select(r => new QuoteResponseDto(
            r.Id, r.Decision, r.SignerName, r.ApprovedTotal, r.Comment, r.IsCurrent, r.CreatedAt,
            r.Items.Select(ri => new QuoteResponseItemDto(ri.QuoteItemId, ri.Approved)).ToList()))
            .ToList();
    }

    public async Task AddAsync(Quote quote, CancellationToken ct) =>
        await _db.Set<Quote>().AddAsync(quote, ct);

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);

    public async Task<IQuoteTransaction> BeginTransactionAsync(CancellationToken ct)
    {
        var tx = await _db.Database.BeginTransactionAsync(ct);
        return new EfQuoteTransaction(tx);
    }

    private sealed class EfQuoteTransaction : IQuoteTransaction
    {
        private readonly Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction _tx;

        public EfQuoteTransaction(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx) => _tx = tx;

        public Task CommitAsync(CancellationToken ct) => _tx.CommitAsync(ct);

        public ValueTask DisposeAsync() => _tx.DisposeAsync();
    }
}
