using System.IO.Compression;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Invoicing;
using TallerControl.Domain.CounterSales;
using TallerControl.Domain.Customers;
using TallerControl.Domain.Invoicing;
using TallerControl.Domain.Purchases;
using TallerControl.Domain.Suppliers;
using TallerControl.Domain.Work;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Invoicing;

/// <summary>
/// Libro IVA mensual y exportación CITI (doc 13 §6-7).
/// Ventas: facturas autorizadas emitidas en el período.
/// Compras: facturas de proveedores confirmadas en el período.
/// </summary>
internal sealed class EfIvaBookRepository : IIvaBookRepository
{
    private readonly AppDbContext _db;

    public EfIvaBookRepository(AppDbContext db) => _db = db;

    public async Task<IvaBookDto> GetIvaBookAsync(int year, int month, CancellationToken ct)
    {
        var from = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddMonths(1);

        var salesEntries = await GetSalesEntriesAsync(from, to, ct);
        var purchasesEntries = await GetPurchasesEntriesAsync(from, to, ct);

        var sales = new IvaBookSalesDto(
            Count: salesEntries.Count,
            TotalNet: salesEntries.Sum(e => e.NetAmount),
            TotalVat: salesEntries.Sum(e => e.VatAmount),
            TotalAmount: salesEntries.Sum(e => e.TotalAmount),
            Entries: salesEntries);

        var purchases = new IvaBookPurchasesDto(
            Count: purchasesEntries.Count,
            TotalNet: purchasesEntries.Sum(e => e.NetAmount),
            TotalVat: purchasesEntries.Sum(e => e.VatAmount),
            TotalAmount: purchasesEntries.Sum(e => e.TotalAmount),
            Entries: purchasesEntries);

        return new IvaBookDto(year, month, sales, purchases);
    }

    public async Task<(byte[] ZipBytes, string FileName)> GetCitiZipAsync(int year, int month, CancellationToken ct)
    {
        var from = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddMonths(1);

        var salesEntries = await GetSalesEntriesAsync(from, to, ct);
        var purchasesEntries = await GetPurchasesEntriesAsync(from, to, ct);

        var fileName = $"CITI_{year:D4}{month:D2}.zip";
        var zipBytes = BuildCitiZip(year, month, salesEntries, purchasesEntries);
        return (zipBytes, fileName);
    }

    private async Task<List<IvaBookEntryDto>> GetSalesEntriesAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var rows = await _db.Set<Invoice>()
            .Where(i => i.Status == InvoiceStatus.Authorized && i.IssuedAt >= from && i.IssuedAt < to)
            .Join(_db.Set<FiscalPointOfSale>(),
                i => i.FiscalPointOfSaleId,
                p => p.Id,
                (i, p) => new
                {
                    i.IssuedAt,
                    PosNumber = p.Number,
                    i.Number,
                    i.InvoiceType,
                    i.NetAmount,
                    i.VatAmount,
                    i.TotalAmount,
                    i.IsCredit,
                    i.Cae,
                    i.SourceType,
                    i.SourceId
                })
            .OrderBy(r => r.IssuedAt)
            .ToListAsync(ct);

        // Cliente de cada comprobante: sale del documento de origen (orden o venta de mostrador).
        var workOrderIds = rows.Where(r => r.SourceType == InvoiceSourceType.WorkOrder).Select(r => r.SourceId).ToList();
        var counterSaleIds = rows.Where(r => r.SourceType == InvoiceSourceType.CounterSale).Select(r => r.SourceId).ToList();

        var workOrderCustomers = await _db.Set<WorkOrder>()
            .Where(w => workOrderIds.Contains(w.Id))
            .Join(_db.Set<Customer>().IgnoreQueryFilters(), w => w.CustomerId, c => c.Id,
                (w, c) => new { w.Id, c.Name, c.DocNumber })
            .ToDictionaryAsync(x => x.Id, x => (x.Name, x.DocNumber), ct);

        var counterSaleCustomers = await _db.Set<CounterSale>()
            .Where(s => counterSaleIds.Contains(s.Id) && s.CustomerId != null)
            .Join(_db.Set<Customer>().IgnoreQueryFilters(), s => s.CustomerId, c => c.Id,
                (s, c) => new { s.Id, c.Name, c.DocNumber })
            .ToDictionaryAsync(x => x.Id, x => (x.Name, x.DocNumber), ct);

        return rows.Select(r =>
        {
            var customers = r.SourceType == InvoiceSourceType.WorkOrder ? workOrderCustomers : counterSaleCustomers;
            customers.TryGetValue(r.SourceId, out var customer);
            // Las notas de crédito restan en el libro (sus importes se informan en negativo).
            var sign = r.IsCredit ? -1m : 1m;
            return new IvaBookEntryDto(
                r.IssuedAt,
                r.Number != null ? $"{r.PosNumber:D4}-{r.Number:D8}" : null,
                customer.DocNumber,
                customer.Name ?? "Consumidor Final",
                r.NetAmount * sign,
                r.VatAmount * sign,
                r.TotalAmount * sign,
                r.IsCredit ? "Nota de crédito" : null,
                r.InvoiceType.ToString(),
                r.Cae);
        }).ToList();
    }

    private async Task<List<IvaBookEntryDto>> GetPurchasesEntriesAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var rows = await _db.Set<Purchase>()
            .Where(p => p.Status == PurchaseStatus.Confirmed
                        && p.InvoiceDate >= from
                        && p.InvoiceDate < to)
            .Join(_db.Set<Supplier>(),
                p => p.SupplierId,
                s => s.Id,
                (p, s) => new
                {
                    p.InvoiceDate,
                    p.PointOfSale,
                    p.InvoiceNumber,
                    p.SubtotalNet,
                    p.VatAmount,
                    p.Total,
                    SupplierCuit = s.Cuit,
                    SupplierName = s.Name
                })
            .OrderBy(r => r.InvoiceDate)
            .ToListAsync(ct);

        return rows.Select(r => new IvaBookEntryDto(
            r.InvoiceDate,
            r.PointOfSale != null && r.InvoiceNumber != null
                ? $"{r.PointOfSale:D4}-{r.InvoiceNumber:D8}"
                : null,
            r.SupplierCuit,
            r.SupplierName,
            r.SubtotalNet,
            r.VatAmount,
            r.Total,
            r.PointOfSale is null || r.InvoiceNumber is null ? "Excluido CITI" : null)
        ).ToList();
    }

    /// <summary>
    /// Genera el ZIP CITI con los 4 archivos de texto de formato fijo.
    /// Formato simplificado para MVP: solo ventas (REGINFO_CV_VENTAS_CBTE) y compras (REGINFO_CV_COMPRAS_CBTE).
    /// </summary>
    private static byte[] BuildCitiZip(
        int year, int month,
        List<IvaBookEntryDto> sales,
        List<IvaBookEntryDto> purchases)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddCitiEntry(zip, $"REGINFO_CV_VENTAS_CBTE.txt", sales, isSales: true);
            AddCitiEntry(zip, $"REGINFO_CV_COMPRAS_CBTE.txt", purchases, isSales: false);
        }
        return ms.ToArray();
    }

    private static void AddCitiEntry(ZipArchive zip, string entryName, List<IvaBookEntryDto> entries, bool isSales)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), Encoding.ASCII);

        foreach (var e in entries)
        {
            // Formato CITI simplificado: fecha|numero|cuit_contraparte|neto|iva|total
            var date = e.Date.ToString("yyyyMMdd");
            var number = (e.InvoiceNumber ?? "0000-00000000").PadLeft(13);
            var cuit = (e.CounterpartyCuit ?? "00000000000").Replace("-", "").PadLeft(11);
            var net = FormatAmount(e.NetAmount);
            var vat = FormatAmount(e.VatAmount);
            var total = FormatAmount(e.TotalAmount);

            writer.WriteLine($"{date}|{number}|{cuit}|{net}|{vat}|{total}");
        }
    }

    // El formato simplificado no admite signo: los importes se informan en valor absoluto.
    private static string FormatAmount(decimal amount) =>
        ((long)(Math.Abs(amount) * 100)).ToString().PadLeft(15, '0');
}
