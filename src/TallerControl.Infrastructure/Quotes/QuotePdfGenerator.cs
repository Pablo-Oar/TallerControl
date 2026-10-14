using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TallerControl.Application.Quotes;
using TallerControl.Domain.Quotes;

namespace TallerControl.Infrastructure.Quotes;

/// <summary>
/// Genera el PDF de un presupuesto con QuestPDF Community (D-05, T-DOC-01, REQ-16).
/// QuestPDF Community requiere mención de la licencia en el pie: MIT, documentado.
/// </summary>
internal sealed class QuotePdfGenerator : IQuotePdfGenerator
{
    public Task<byte[]> GenerateAsync(QuoteDto quote, CancellationToken ct)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(10).FontFamily("Arial"));

                page.Header().Element(c => BuildHeader(c, quote));
                page.Content().Element(c => BuildContent(c, quote));
                page.Footer().Element(BuildFooter);
            });
        });

        var bytes = document.GeneratePdf();
        return Task.FromResult(bytes);
    }

    private static void BuildHeader(IContainer container, QuoteDto quote)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text($"Presupuesto #{quote.Number}")
                    .Bold().FontSize(16);
                col.Item().Text(StatusLabel(quote.Status))
                    .FontSize(10).FontColor(Colors.Grey.Darken2);
            });

            row.ConstantItem(150).Column(col =>
            {
                col.Item().AlignRight().Text($"Válido hasta: {quote.ValidUntil:dd/MM/yyyy}")
                    .FontSize(9);
                col.Item().AlignRight().Text($"Emitido: {quote.CreatedAt:dd/MM/yyyy}")
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        });
    }

    private static void BuildContent(IContainer container, QuoteDto quote)
    {
        container.Column(col =>
        {
            // Cliente y vehículo
            col.Item().Padding(5).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("CLIENTE").Bold().FontSize(9).FontColor(Colors.Grey.Darken2);
                    c.Item().Text(quote.CustomerName);
                });

                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("VEHÍCULO").Bold().FontSize(9).FontColor(Colors.Grey.Darken2);
                    c.Item().Text($"{quote.VehicleBrand} {quote.VehicleModel}".Trim());
                    c.Item().Text($"Patente: {quote.VehiclePlate}").FontSize(9);
                });
            });

            col.Item().PaddingVertical(8).LineHorizontal(0.5f);

            // Nota al cliente
            if (!string.IsNullOrEmpty(quote.CustomerNotes))
            {
                col.Item().Padding(5).Column(c =>
                {
                    c.Item().Text("NOTAS").Bold().FontSize(9).FontColor(Colors.Grey.Darken2);
                    c.Item().Text(quote.CustomerNotes).FontSize(9);
                });
                col.Item().PaddingVertical(4).LineHorizontal(0.5f);
            }

            // Tabla de ítems
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(4);
                    cols.RelativeColumn(1);
                    cols.RelativeColumn(1.5f);
                    cols.RelativeColumn(1);
                    cols.RelativeColumn(1.5f);
                });

                table.Header(header =>
                {
                    header.Cell().Text("Descripción").Bold().FontSize(9);
                    header.Cell().AlignRight().Text("Cant.").Bold().FontSize(9);
                    header.Cell().AlignRight().Text("P. Unitario").Bold().FontSize(9);
                    header.Cell().AlignRight().Text("IVA %").Bold().FontSize(9);
                    header.Cell().AlignRight().Text("Total").Bold().FontSize(9);
                    header.Cell().ColumnSpan(5).PaddingTop(2).LineHorizontal(0.5f);
                });

                foreach (var item in quote.Items.OrderBy(i => i.Position))
                {
                    table.Cell().Text(item.Description).FontSize(9);
                    table.Cell().AlignRight().Text($"{item.Quantity:N2}").FontSize(9);
                    table.Cell().AlignRight().Text($"$ {item.UnitNetPrice:N2}").FontSize(9);
                    table.Cell().AlignRight().Text($"{item.VatRate:N0} %").FontSize(9);
                    table.Cell().AlignRight().Text($"$ {item.LineTotal:N2}").FontSize(9);
                }
            });

            col.Item().PaddingVertical(6).LineHorizontal(0.5f);

            // Totales
            col.Item().AlignRight().Column(c =>
            {
                c.Item().Text($"Subtotal neto: $ {quote.SubtotalNet:N2}").FontSize(9);
                if (quote.GlobalDiscountPct > 0)
                    c.Item().Text($"Descuento ({quote.GlobalDiscountPct:N1} %): -$ {quote.DiscountAmount:N2}").FontSize(9);
                c.Item().Text($"IVA: $ {quote.VatAmount:N2}").FontSize(9);
                c.Item().PaddingTop(2).Text($"TOTAL: $ {quote.Total:N2}").Bold().FontSize(12);
            });
        });
    }

    private static void BuildFooter(IContainer container)
    {
        container.AlignCenter()
            .Text(t =>
            {
                t.Span("Generado por TallerControl · PDF generado con ").FontSize(7).FontColor(Colors.Grey.Medium);
                t.Hyperlink("QuestPDF", "https://www.questpdf.com").FontSize(7).FontColor(Colors.Grey.Medium);
                t.Span(" (MIT License)").FontSize(7).FontColor(Colors.Grey.Medium);
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
