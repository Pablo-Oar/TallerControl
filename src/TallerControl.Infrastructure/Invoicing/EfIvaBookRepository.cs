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
            .Where(i => i.Status == InvoiceStatus.Authorized && !i.IsHomologation && i.IssuedAt >= from && i.IssuedAt < to)
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
                (w, c) => new { w.Id, c.Name, c.DocNumber, c.DocType })
            .ToDictionaryAsync(x => x.Id, x => (x.Name, x.DocNumber, x.DocType), ct);

        var counterSaleCustomers = await _db.Set<CounterSale>()
            .Where(s => counterSaleIds.Contains(s.Id) && s.CustomerId != null)
            .Join(_db.Set<Customer>().IgnoreQueryFilters(), s => s.CustomerId, c => c.Id,
                (s, c) => new { s.Id, c.Name, c.DocNumber, c.DocType })
            .ToDictionaryAsync(x => x.Id, x => (x.Name, x.DocNumber, x.DocType), ct);

        // Alícuotas de cada comprobante, agrupando los renglones del documento de origen.
        var workOrderRates = (await _db.Set<WorkOrderItem>()
                .Where(i => workOrderIds.Contains(i.WorkOrderId))
                .Select(i => new { i.WorkOrderId, i.VatRate, i.LineNet, i.LineVat })
                .ToListAsync(ct))
            .GroupBy(i => i.WorkOrderId)
            .ToDictionary(g => g.Key, g => g
                .GroupBy(i => i.VatRate)
                .Select(x => new IvaRateLineDto(x.Key, x.Sum(i => i.LineNet), x.Sum(i => i.LineVat)))
                .ToList());

        var counterSaleRates = (await _db.Set<CounterSaleItem>()
                .Where(i => counterSaleIds.Contains(i.CounterSaleId))
                .Select(i => new { i.CounterSaleId, i.VatRate, i.NetAmount, i.VatAmount })
                .ToListAsync(ct))
            .GroupBy(i => i.CounterSaleId)
            .ToDictionary(g => g.Key, g => g
                .GroupBy(i => i.VatRate)
                .Select(x => new IvaRateLineDto(x.Key, x.Sum(i => i.NetAmount), x.Sum(i => i.VatAmount)))
                .ToList());

        return rows.Select(r =>
        {
            var customers = r.SourceType == InvoiceSourceType.WorkOrder ? workOrderCustomers : counterSaleCustomers;
            customers.TryGetValue(r.SourceId, out var customer);
            var rates = r.SourceType == InvoiceSourceType.WorkOrder ? workOrderRates : counterSaleRates;
            rates.TryGetValue(r.SourceId, out var rateLines);
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
                r.Cae,
                r.IsCredit,
                customer.DocType?.ToString(),
                0m,
                Reconcile(rateLines, r.NetAmount, r.VatAmount));
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
                    p.Perceptions,
                    p.Id,
                    SupplierCuit = s.Cuit,
                    SupplierName = s.Name,
                    SupplierVat = s.VatCondition
                })
            .OrderBy(r => r.InvoiceDate)
            .ToListAsync(ct);

        var purchaseIds = rows.Select(r => r.Id).ToList();
        var purchaseRates = (await _db.Set<PurchaseItem>()
                .Where(i => purchaseIds.Contains(i.PurchaseId))
                .Select(i => new { i.PurchaseId, i.Quantity, i.UnitCost, i.VatRate })
                .ToListAsync(ct))
            .GroupBy(i => i.PurchaseId)
            .ToDictionary(g => g.Key, g => g
                .GroupBy(i => i.VatRate)
                .Select(x =>
                {
                    var net = x.Sum(i => Math.Round(i.Quantity * i.UnitCost, 2, MidpointRounding.AwayFromZero));
                    return new IvaRateLineDto(x.Key, net, Math.Round(net * x.Key / 100m, 2, MidpointRounding.AwayFromZero));
                })
                .ToList());

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
            r.PointOfSale is null || r.InvoiceNumber is null ? "Excluido CITI" : null,
            // El proveedor responsable inscripto factura A; el resto, C (la compra no guarda la letra).
            InvoiceType: r.SupplierVat == VatCondition.ResponsableInscripto ? "A" : "C",
            Perceptions: r.Perceptions,
            RateLines: Reconcile(purchaseRates.GetValueOrDefault(r.Id), r.SubtotalNet, r.VatAmount))
        ).ToList();
    }

    /// <summary>
    /// Ajusta las alícuotas agrupadas para que sumen exactamente el neto y el IVA del comprobante
    /// (la diferencia, por redondeo o descuentos, va a la alícuota más grande).
    /// </summary>
    private static IReadOnlyList<IvaRateLineDto>? Reconcile(List<IvaRateLineDto>? lines, decimal net, decimal vat)
    {
        if (lines is not { Count: > 0 }) return null;

        var adjusted = lines.OrderBy(l => l.VatRate).ToList();
        var big = adjusted.IndexOf(adjusted.OrderByDescending(l => l.Net).First());
        var netDiff = net - adjusted.Sum(l => l.Net);
        var vatDiff = vat - adjusted.Sum(l => l.Vat);
        adjusted[big] = adjusted[big] with { Net = adjusted[big].Net + netDiff, Vat = adjusted[big].Vat + vatDiff };
        return adjusted;
    }

    /// <summary>
    /// Genera el ZIP CITI con los registros de Compras y Ventas en el diseño oficial de ancho fijo (D-32):
    /// cabecera y alícuotas de ventas, cabecera y alícuotas de compras.
    /// </summary>
    private static byte[] BuildCitiZip(
        int year, int month,
        List<IvaBookEntryDto> sales,
        List<IvaBookEntryDto> purchases)
    {
        // Un comprobante sin punto de venta y número no se puede informar: queda afuera del archivo y se lista
        // en LEEME.txt para que se complete la factura del proveedor.
        var includedSales = sales.Where(e => e.InvoiceNumber is not null).ToList();
        var includedPurchases = purchases.Where(e => e.InvoiceNumber is not null).ToList();
        var excluded = purchases.Where(e => e.InvoiceNumber is null).ToList();

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddLines(zip, "REGINFO_CV_VENTAS_CBTE.txt", includedSales.Select(CitiOfficialFormat.SalesHeader));
            AddLines(zip, "REGINFO_CV_VENTAS_ALICUOTAS.txt", includedSales.SelectMany(CitiOfficialFormat.SalesRates));
            AddLines(zip, "REGINFO_CV_COMPRAS_CBTE.txt", includedPurchases.Select(CitiOfficialFormat.PurchasesHeader));
            AddLines(zip, "REGINFO_CV_COMPRAS_ALICUOTAS.txt", includedPurchases.SelectMany(CitiOfficialFormat.PurchasesRates));
            AddReadme(zip, year, month, excluded);
        }
        return ms.ToArray();
    }

    private static void AddReadme(ZipArchive zip, int year, int month, List<IvaBookEntryDto> excludedPurchases)
    {
        var entry = zip.CreateEntry("LEEME.txt", CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.WriteLine($"TallerControl - exportación CITI {year:D4}-{month:D2}");
        writer.WriteLine();
        writer.WriteLine("Registros de Compras y Ventas (RG 3685) en el diseño de ancho fijo de ARCA:");
        writer.WriteLine("  REGINFO_CV_VENTAS_CBTE.txt        (266 caracteres por línea)");
        writer.WriteLine("  REGINFO_CV_VENTAS_ALICUOTAS.txt   (62)");
        writer.WriteLine("  REGINFO_CV_COMPRAS_CBTE.txt       (325)");
        writer.WriteLine("  REGINFO_CV_COMPRAS_ALICUOTAS.txt  (84)");
        writer.WriteLine();
        writer.WriteLine("Antes de presentar, importá los archivos en el aplicativo / validador de ARCA y revisalos con tu contador.");
        writer.WriteLine("Las facturas emitidas en homologación (de prueba) no se incluyen.");
        writer.WriteLine("La letra de las facturas de compra se deduce de la condición frente al IVA del proveedor (responsable inscripto = A, resto = C).");
        if (excludedPurchases.Count == 0) return;
        writer.WriteLine();
        writer.WriteLine("Compras que NO se incluyeron por falta de punto de venta o número de factura:");
        foreach (var e in excludedPurchases)
            writer.WriteLine($"- {e.Date:yyyy-MM-dd} {e.CounterpartyName} total {e.TotalAmount:0.00}");
    }

    private static void AddLines(ZipArchive zip, string entryName, IEnumerable<string> lines)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), Encoding.ASCII) { NewLine = "\r\n" };
        foreach (var line in lines)
            writer.WriteLine(line);
    }
}
