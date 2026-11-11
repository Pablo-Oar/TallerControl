using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TallerControl.Application.Common;
using TallerControl.Application.Quotes;
using TallerControl.Domain.Quotes;
using TallerControl.Infrastructure.Branding;

namespace TallerControl.Infrastructure.Quotes;

/// <summary>
/// Genera el PDF de un presupuesto con QuestPDF Community (D-05, T-DOC-01, REQ-16).
/// QuestPDF Community requiere mención de la licencia en el pie: MIT, documentado.
/// </summary>
internal sealed class QuotePdfGenerator : IQuotePdfGenerator
{
    private readonly IWorkshopBranding _branding;
    private readonly ITenantContext _tenant;

    public QuotePdfGenerator(IWorkshopBranding branding, ITenantContext tenant)
    {
        _branding = branding;
        _tenant = tenant;
    }

    public async Task<byte[]> GenerateAsync(QuoteDto quote, CancellationToken ct)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var logo = _tenant.TenantId is { } tenantId ? (await _branding.GetLogoAsync(tenantId, ct))?.Bytes : null;
        return PdfLogo.Generate(l => Build(quote, l), logo);
    }

    private static byte[] Build(QuoteDto quote, byte[]? logo)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                PdfStyle.ApplyPage(page);

                page.Header().Element(c => PdfStyle.Header(
                    c, logo,
                    $"Presupuesto #{quote.Number}",
                    StatusLabel(quote.Status),
                    $"Válido hasta: {quote.ValidUntil:dd/MM/yyyy}",
                    $"Emitido: {quote.CreatedAt:dd/MM/yyyy}"));
                page.Content().PaddingTop(4).Element(c => BuildContent(c, quote));
                page.Footer().Element(PdfStyle.Footer);
            });
        });

        return document.GeneratePdf();
    }

    private static void BuildContent(IContainer container, QuoteDto quote)
    {
        container.Column(col =>
        {
            col.Spacing(16);

            col.Item().Element(PdfStyle.InfoBox).Row(row =>
            {
                row.Spacing(12);
                PdfStyle.Field(row, "Cliente", quote.CustomerName);
                PdfStyle.Field(
                    row, "Vehículo", $"{quote.VehicleBrand} {quote.VehicleModel}".Trim(),
                    $"Patente: {quote.VehiclePlate}");
            });

            if (!string.IsNullOrEmpty(quote.CustomerNotes))
            {
                col.Item().Column(c =>
                {
                    PdfStyle.SectionTitle(c, "Notas");
                    c.Item().BorderLeft(2).BorderColor(PdfStyle.Violet).PaddingLeft(8)
                        .Text(quote.CustomerNotes).LineHeight(1.35f);
                });
            }

            col.Item().Column(c =>
            {
                PdfStyle.SectionTitle(c, "Detalle");

                c.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(4.5f);
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(1.2f);
                        cols.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(x => PdfStyle.HeadText(x, "Descripción"));
                        header.Cell().Element(x => PdfStyle.HeadText(x, "Cant.", true));
                        header.Cell().Element(x => PdfStyle.HeadText(x, "P. unitario", true));
                        header.Cell().Element(x => PdfStyle.HeadText(x, "IVA", true));
                        header.Cell().Element(x => PdfStyle.HeadText(x, "Total", true));
                    });

                    var i = 0;
                    foreach (var item in quote.Items.OrderBy(it => it.Position))
                    {
                        var idx = i++;
                        table.Cell().Element(x => PdfStyle.BodyCell(x, idx)).Text(item.Description);
                        table.Cell().Element(x => PdfStyle.BodyCell(x, idx)).AlignRight().Text($"{item.Quantity:0.###}");
                        table.Cell().Element(x => PdfStyle.BodyCell(x, idx)).AlignRight().Text($"$ {item.UnitNetPrice:N2}");
                        table.Cell().Element(x => PdfStyle.BodyCell(x, idx)).AlignRight().Text($"{item.VatRate:0.##} %");
                        table.Cell().Element(x => PdfStyle.BodyCell(x, idx)).AlignRight().Text($"$ {item.LineTotal:N2}");
                    }
                });

                c.Item().PaddingTop(10).AlignRight().Width(220).Column(totals =>
                {
                    PdfStyle.TotalLine(totals, "Subtotal neto", $"$ {quote.SubtotalNet:N2}");
                    if (quote.GlobalDiscountPct > 0)
                        PdfStyle.TotalLine(totals, $"Descuento ({quote.GlobalDiscountPct:N1} %)", $"-$ {quote.DiscountAmount:N2}");
                    PdfStyle.TotalLine(totals, "IVA", $"$ {quote.VatAmount:N2}");
                    PdfStyle.GrandTotal(totals, "TOTAL", $"$ {quote.Total:N2}");
                });
            });
        });
    }

    private static string StatusLabel(QuoteStatus status) => status switch
    {
        QuoteStatus.Draft => "Borrador",
        QuoteStatus.Sent => "Enviado",
        QuoteStatus.Approved => "Aprobado",
        QuoteStatus.PartiallyApproved => "Aprobado parcialmente",
        QuoteStatus.Rejected => "Rechazado",
        QuoteStatus.Expired => "Vencido",
        QuoteStatus.Converted => "Convertido a OT",
        QuoteStatus.Cancelled => "Cancelado",
        _ => status.ToString(),
    };
}
