using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TallerControl.Application.Invoicing;
using TallerControl.Domain.CounterSales;
using TallerControl.Domain.Customers;
using TallerControl.Domain.Invoicing;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Work;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Invoicing;

/// <summary>
/// PDF del comprobante electrónico autorizado por ARCA (F-01): datos del emisor y del receptor, renglones, totales,
/// CAE con su vencimiento y el código QR fiscal (RG 4291). Un comprobante emitido en homologación sale marcado como
/// de prueba y sin QR.
/// </summary>
internal sealed class InvoicePdfGenerator : IInvoicePdfGenerator
{
    private readonly AppDbContext _db;

    public InvoicePdfGenerator(AppDbContext db) => _db = db;

    public async Task<byte[]> GenerateAsync(Guid invoiceId, CancellationToken ct)
    {
        var invoice = await _db.Set<Invoice>().AsNoTracking()
            .Include(i => i.FiscalPointOfSale).ThenInclude(p => p.FiscalCertificate)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct)
            ?? throw new InvalidOperationException($"Comprobante {invoiceId} no encontrado.");

        var tenant = await _db.Set<Tenant>().AsNoTracking().IgnoreQueryFilters()
            .FirstAsync(t => t.Id == invoice.TenantId, ct);

        var data = await LoadSourceAsync(invoice, ct);
        var original = invoice.OriginalInvoiceId is { } originalId
            ? await _db.Set<Invoice>().AsNoTracking().Include(i => i.FiscalPointOfSale)
                .FirstOrDefaultAsync(i => i.Id == originalId, ct)
            : null;

        QuestPDF.Settings.License = LicenseType.Community;
        var model = new InvoiceView(invoice, tenant, data, original);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(9).FontFamily("Arial"));

                page.Header().Element(c => BuildHeader(c, model));
                page.Content().Element(c => BuildContent(c, model));
                page.Footer().Element(c => BuildFooter(c, model));
            });
        });

        return document.GeneratePdf();
    }

    // ── Datos ────────────────────────────────────────────────────────────────

    private async Task<SourceView> LoadSourceAsync(Invoice invoice, CancellationToken ct)
    {
        Customer? customer = null;
        var lines = new List<Line>();
        string reference;

        if (invoice.SourceType == InvoiceSourceType.WorkOrder)
        {
            var wo = await _db.Set<WorkOrder>().AsNoTracking().FirstAsync(w => w.Id == invoice.SourceId, ct);
            customer = await _db.Set<Customer>().AsNoTracking().IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == wo.CustomerId, ct);
            reference = $"Orden de trabajo N° {wo.Number}";
            lines = await _db.Set<WorkOrderItem>().AsNoTracking()
                .Where(i => i.WorkOrderId == wo.Id)
                .OrderBy(i => i.Position)
                .Select(i => new Line(i.Description, i.Quantity, i.VatRate, i.LineNet, i.LineVat, i.LineTotal))
                .ToListAsync(ct);
        }
        else
        {
            var sale = await _db.Set<CounterSale>().AsNoTracking().FirstAsync(s => s.Id == invoice.SourceId, ct);
            if (sale.CustomerId is { } customerId)
                customer = await _db.Set<Customer>().AsNoTracking().IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == customerId, ct);
            reference = "Venta de mostrador";
            lines = await _db.Set<CounterSaleItem>().AsNoTracking()
                .Where(i => i.CounterSaleId == sale.Id)
                .Select(i => new Line(i.Description, i.Quantity, i.VatRate, i.NetAmount, i.VatAmount, i.Total))
                .ToListAsync(ct);
        }

        return new SourceView(customer, lines, reference);
    }

    // ── Diseño ───────────────────────────────────────────────────────────────

    private static void BuildHeader(IContainer container, InvoiceView m)
    {
        var inv = m.Invoice;
        var letter = inv.InvoiceType.ToString().ToUpperInvariant();
        var code = CitiOfficialFormat.VoucherCode(letter, inv.IsCredit);
        var number = $"{inv.FiscalPointOfSale.Number:D5}-{inv.Number ?? 0:D8}";
        var cuit = inv.FiscalPointOfSale.FiscalCertificate.Cuit;

        container.Column(col =>
        {
            if (inv.IsHomologation)
            {
                col.Item().Background(Colors.Amber.Lighten3).Padding(4).AlignCenter()
                    .Text("COMPROBANTE DE PRUEBA (HOMOLOGACIÓN) — SIN VALIDEZ FISCAL")
                    .Bold().FontColor(Colors.Red.Darken2);
            }

            col.Item().Border(1).BorderColor(Colors.Grey.Medium).Row(row =>
            {
                row.RelativeItem().Padding(8).Column(c =>
                {
                    c.Item().Text(m.Tenant.LegalName ?? m.Tenant.Name).Bold().FontSize(14);
                    if (!string.IsNullOrWhiteSpace(m.Tenant.Address))
                        c.Item().Text($"Domicilio: {m.Tenant.Address}");
                    if (!string.IsNullOrWhiteSpace(m.Tenant.Phone))
                        c.Item().Text($"Teléfono: {m.Tenant.Phone}");
                    c.Item().Text($"CUIT: {cuit}");
                });

                row.ConstantItem(70).Border(1).BorderColor(Colors.Grey.Medium).Padding(4).Column(c =>
                {
                    c.Item().AlignCenter().Text(letter).Bold().FontSize(30);
                    c.Item().AlignCenter().Text($"COD. {code:D2}").FontSize(8);
                });

                row.RelativeItem().Padding(8).Column(c =>
                {
                    c.Item().Text(inv.IsCredit ? "NOTA DE CRÉDITO" : "FACTURA").Bold().FontSize(14);
                    c.Item().Text($"N° {number}").Bold();
                    c.Item().Text($"Fecha de emisión: {inv.IssuedAt.ToLocalTime():dd/MM/yyyy}");
                    c.Item().Text($"Punto de venta: {inv.FiscalPointOfSale.Number:D5}");
                });
            });
        });
    }

    private static void BuildContent(IContainer container, InvoiceView m)
    {
        var inv = m.Invoice;
        var discriminates = inv.InvoiceType is InvoiceType.A or InvoiceType.M;

        container.PaddingTop(8).Column(col =>
        {
            col.Spacing(8);

            // Receptor
            col.Item().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(6).Column(c =>
            {
                var customer = m.Source.Customer;
                c.Item().Text(text =>
                {
                    text.Span("Cliente: ").Bold();
                    text.Span(customer?.Name ?? "Consumidor Final");
                });
                c.Item().Text(text =>
                {
                    text.Span("Documento: ").Bold();
                    text.Span(string.IsNullOrWhiteSpace(customer?.DocNumber)
                        ? "—"
                        : $"{DocLabel(customer.DocType)} {customer.DocNumber}");
                    text.Span("     Condición frente al IVA: ").Bold();
                    text.Span(VatLabel(customer?.VatCondition ?? VatCondition.ConsumidorFinal));
                });
                if (!string.IsNullOrWhiteSpace(customer?.Address))
                    c.Item().Text($"Domicilio: {customer.Address}");
                c.Item().Text($"Referencia: {m.Source.Reference}").FontColor(Colors.Grey.Darken1);
                if (m.Original is { } original)
                    c.Item().Text($"Anula el comprobante {original.FiscalPointOfSale.Number:D5}-{original.Number ?? 0:D8}")
                        .FontColor(Colors.Grey.Darken1);
            });

            // Renglones
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(5);
                    cols.ConstantColumn(45);
                    cols.ConstantColumn(70);
                    if (discriminates) cols.ConstantColumn(40);
                    cols.ConstantColumn(75);
                });

                void Head(string text, bool right = false)
                {
                    var cell = table.Cell().Background(Colors.Grey.Lighten3).Padding(4);
                    (right ? cell.AlignRight() : cell).Text(text).Bold().FontSize(8);
                }

                Head("Descripción");
                Head("Cant.", true);
                Head(discriminates ? "Precio unit. (neto)" : "Precio unit.", true);
                if (discriminates) Head("IVA %", true);
                Head("Subtotal", true);

                foreach (var line in m.Source.Lines)
                {
                    var qty = line.Quantity == 0 ? 1 : line.Quantity;
                    var unit = (discriminates ? line.Net : line.Total) / qty;

                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(line.Description);
                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(Number(line.Quantity));
                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text(Money(unit));
                    if (discriminates)
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight().Text($"{Number(line.VatRate)}");
                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).AlignRight()
                        .Text(Money(discriminates ? line.Net : line.Total));
                }

                // Diferencia entre los renglones y el comprobante (descuentos, redondeos).
                var linesTotal = m.Source.Lines.Sum(l => discriminates ? l.Net : l.Total);
                var target = discriminates ? inv.NetAmount : inv.TotalAmount;
                var diff = target - linesTotal;
                if (Math.Abs(diff) >= 0.01m)
                {
                    var span = discriminates ? 4 : 3;
                    table.Cell().ColumnSpan((uint)span).Padding(3).Text("Ajuste / descuento");
                    table.Cell().Padding(3).AlignRight().Text(Money(diff));
                }
            });

            // Totales
            col.Item().AlignRight().Width(230).Column(c =>
            {
                if (discriminates)
                {
                    TotalRow(c, "Importe neto gravado", inv.NetAmount);
                    TotalRow(c, "IVA", inv.VatAmount);
                }
                else if (inv.VatAmount > 0)
                {
                    c.Item().AlignRight().Text($"IVA contenido: {Money(inv.VatAmount)}").FontSize(8).FontColor(Colors.Grey.Darken1);
                }

                c.Item().Background(Colors.Grey.Lighten3).Padding(4).Row(r =>
                {
                    r.RelativeItem().Text("TOTAL").Bold().FontSize(11);
                    r.ConstantItem(100).AlignRight().Text(Money(inv.TotalAmount)).Bold().FontSize(11);
                });
            });
        });
    }

    private static void TotalRow(ColumnDescriptor c, string label, decimal amount) =>
        c.Item().Padding(2).Row(r =>
        {
            r.RelativeItem().Text(label);
            r.ConstantItem(100).AlignRight().Text(Money(amount));
        });

    private static void BuildFooter(IContainer container, InvoiceView m)
    {
        var inv = m.Invoice;

        container.Column(col =>
        {
            col.Item().BorderTop(1).BorderColor(Colors.Grey.Medium).PaddingTop(6).Row(row =>
            {
                if (!inv.IsHomologation && inv.Cae is not null)
                {
                    row.ConstantItem(90).Height(90).Image(BuildQr(m));
                }

                row.RelativeItem().PaddingLeft(8).Column(c =>
                {
                    c.Item().Text(text =>
                    {
                        text.Span("CAE N°: ").Bold();
                        text.Span(inv.Cae ?? "—");
                    });
                    c.Item().Text(text =>
                    {
                        text.Span("Fecha de vencimiento del CAE: ").Bold();
                        text.Span(inv.CaeExpiresAt?.ToString("dd/MM/yyyy") ?? "—");
                    });
                    c.Item().PaddingTop(4).Text(inv.IsHomologation
                        ? "Comprobante emitido contra el ambiente de homologación de ARCA: no tiene validez fiscal."
                        : "Comprobante autorizado por ARCA. Verificá su autenticidad con el código QR.")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                });

                row.ConstantItem(100).AlignRight().AlignBottom().Text("Generado con TallerControl")
                    .FontSize(7).FontColor(Colors.Grey.Darken1);
            });
        });
    }

    // ── QR fiscal (RG 4291) ──────────────────────────────────────────────────

    internal static string BuildQrUrl(Invoice inv, Customer? customer)
    {
        var letter = inv.InvoiceType.ToString().ToUpperInvariant();
        var hasDoc = !string.IsNullOrWhiteSpace(customer?.DocNumber);
        var docDigits = hasDoc ? new string(customer!.DocNumber!.Where(char.IsDigit).ToArray()) : string.Empty;

        var payload = new
        {
            ver = 1,
            fecha = inv.IssuedAt.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            cuit = long.Parse(new string(inv.FiscalPointOfSale.FiscalCertificate.Cuit.Where(char.IsDigit).ToArray()), CultureInfo.InvariantCulture),
            ptoVta = inv.FiscalPointOfSale.Number,
            tipoCmp = (int)CitiOfficialFormat.VoucherCode(letter, inv.IsCredit),
            nroCmp = inv.Number ?? 0,
            importe = Math.Round(inv.TotalAmount, 2),
            moneda = "PES",
            ctz = 1,
            tipoDocRec = hasDoc ? DocCode(customer!.DocType) : 99,
            nroDocRec = hasDoc && long.TryParse(docDigits, out var n) ? n : 0L,
            tipoCodAut = "E",
            codAut = long.TryParse(inv.Cae, out var cae) ? cae : 0L,
        };

        var json = JsonSerializer.Serialize(payload);
        return "https://www.afip.gob.ar/fe/qr/?p=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    private static byte[] BuildQr(InvoiceView m)
    {
        using var generator = new QRCodeGenerator();
        var data = generator.CreateQrCode(BuildQrUrl(m.Invoice, m.Source.Customer), QRCodeGenerator.ECCLevel.M);
        using var png = new PngByteQRCode(data);
        return png.GetGraphic(8);
    }

    // ── Formato ──────────────────────────────────────────────────────────────

    private static int DocCode(DocType? type) => type switch
    {
        DocType.Cuit => 80,
        DocType.Cuil => 86,
        DocType.Dni => 96,
        DocType.Pasaporte => 94,
        _ => 99,
    };

    private static string DocLabel(DocType? type) => type switch
    {
        DocType.Cuit => "CUIT",
        DocType.Cuil => "CUIL",
        DocType.Dni => "DNI",
        DocType.Pasaporte => "Pasaporte",
        _ => "Doc.",
    };

    private static string VatLabel(VatCondition condition) => condition switch
    {
        VatCondition.ResponsableInscripto => "Responsable Inscripto",
        VatCondition.Monotributo => "Monotributo",
        VatCondition.Exento => "IVA Exento",
        VatCondition.NoResponsable => "No Responsable",
        _ => "Consumidor Final",
    };

    private static string Money(decimal value) => "$ " + value.ToString("N2", CultureInfo.GetCultureInfo("es-AR"));

    private static string Number(decimal value) => value.ToString("0.###", CultureInfo.GetCultureInfo("es-AR"));

    private sealed record Line(string Description, decimal Quantity, decimal VatRate, decimal Net, decimal Vat, decimal Total);

    private sealed record SourceView(Customer? Customer, List<Line> Lines, string Reference);

    private sealed record InvoiceView(Invoice Invoice, Tenant Tenant, SourceView Source, Invoice? Original);
}
