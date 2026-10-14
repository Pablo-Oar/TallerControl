using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TallerControl.Application.Work;
using TallerControl.Domain.Work;

namespace TallerControl.Infrastructure.Work;

/// <summary>
/// Genera el PDF de una OT con QuestPDF Community (D-05, T-DOC-02, REQ-16).
/// QuestPDF Community requiere mención de la licencia en el pie: MIT, documentado.
/// </summary>
internal sealed class WorkOrderPdfGenerator : IWorkOrderPdfGenerator
{
    public Task<byte[]> GenerateAsync(WorkOrderDto order, CancellationToken ct)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(10).FontFamily("Arial"));

                page.Header().Element(c => BuildHeader(c, order));
                page.Content().Element(c => BuildContent(c, order));
                page.Footer().Element(BuildFooter);
            });
        });

        var bytes = document.GeneratePdf();
        return Task.FromResult(bytes);
    }

    private static void BuildHeader(IContainer container, WorkOrderDto order)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text($"Orden de Trabajo #{order.Number}")
                    .Bold().FontSize(16);
                col.Item().Text(StatusLabel(order.Status))
                    .FontSize(10).FontColor(Colors.Grey.Darken2);
            });

            row.ConstantItem(150).Column(col =>
            {
                col.Item().AlignRight().Text($"Prioridad: {PriorityLabel(order.Priority)}")
                    .FontSize(9);
                col.Item().AlignRight().Text($"Creada: {order.CreatedAt:dd/MM/yyyy}")
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        });
    }

    private static void BuildContent(IContainer container, WorkOrderDto order)
    {
        container.Column(col =>
        {
            col.Spacing(10);

            // Vehículo y cliente
            col.Item().Element(c => BuildInfoSection(c, order));

            // Problema reportado
            if (!string.IsNullOrWhiteSpace(order.ReportedIssue))
            {
                col.Item().Element(c => BuildProblemSection(c, order.ReportedIssue));
            }

            // Renglones
            if (order.Items.Count > 0)
            {
                col.Item().Element(c => BuildItemsTable(c, order));
            }

            // Notas mecánico / internas
            if (!string.IsNullOrWhiteSpace(order.MechanicNotes))
            {
                col.Item().Element(c => BuildNotesSection(c, "Notas del mecánico", order.MechanicNotes));
            }

            if (!string.IsNullOrWhiteSpace(order.InternalNotes))
            {
                col.Item().Element(c => BuildNotesSection(c, "Notas internas", order.InternalNotes));
            }
        });
    }

    private static void BuildInfoSection(IContainer container, WorkOrderDto order)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Row(row =>
        {
            // Cliente
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("Cliente").Bold().FontSize(9).FontColor(Colors.Grey.Darken2);
                col.Item().Text(order.CustomerName);
            });

            // Vehículo
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("Vehículo").Bold().FontSize(9).FontColor(Colors.Grey.Darken2);
                col.Item().Text(order.VehiclePlate).FontFamily("Courier New");
                if (!string.IsNullOrEmpty(order.VehicleBrand))
                    col.Item().Text($"{order.VehicleBrand} {order.VehicleModel}".Trim())
                        .FontColor(Colors.Grey.Darken1);
            });

            // Odómetro
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("Odómetro").Bold().FontSize(9).FontColor(Colors.Grey.Darken2);
                col.Item().Text($"{order.OdometerKm:N0} km");
                if (order.FuelLevel.HasValue)
                    col.Item().Text($"Combustible: {order.FuelLevel}%").FontColor(Colors.Grey.Darken1);
            });

            // Mecánico
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("Mecánico").Bold().FontSize(9).FontColor(Colors.Grey.Darken2);
                col.Item().Text(order.AssignedMechanicName ?? "—");
            });
        });
    }

    private static void BuildProblemSection(IContainer container, string issue)
    {
        container.Column(col =>
        {
            col.Item().Text("Problema reportado").Bold().FontSize(9).FontColor(Colors.Grey.Darken2);
            col.Item().PaddingTop(3).Text(issue);
        });
    }

    private static void BuildItemsTable(IContainer container, WorkOrderDto order)
    {
        container.Column(col =>
        {
            col.Item().Text("Renglones").Bold().FontSize(9).FontColor(Colors.Grey.Darken2);

            col.Item().PaddingTop(3).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(4); // descripción
                    columns.RelativeColumn(1); // cantidad
                    columns.RelativeColumn(2); // precio neto
                    columns.RelativeColumn(1); // iva %
                    columns.RelativeColumn(2); // total línea
                });

                // Encabezado
                table.Header(header =>
                {
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Descripción").Bold().FontSize(8);
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Cant.").Bold().FontSize(8);
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("P. neto").Bold().FontSize(8);
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("IVA %").Bold().FontSize(8);
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Total").Bold().FontSize(8);
                });

                // Filas
                foreach (var item in order.Items)
                {
                    table.Cell().Padding(4).Text(item.Description);
                    table.Cell().Padding(4).AlignRight().Text(item.Quantity.ToString("G29"));
                    table.Cell().Padding(4).AlignRight().Text(
                        order.SubtotalNet.HasValue ? $"${item.UnitNetPrice:N2}" : "—");
                    table.Cell().Padding(4).AlignRight().Text($"{item.VatRate}%");
                    table.Cell().Padding(4).AlignRight().Text(
                        order.Total.HasValue ? $"${item.LineTotal:N2}" : "—");
                }
            });

            // Totales
            if (order.Total.HasValue)
            {
                col.Item().PaddingTop(4).AlignRight().Column(totals =>
                {
                    totals.Item().Text($"Subtotal neto: ${order.SubtotalNet:N2}").FontSize(9);
                    totals.Item().Text($"IVA: ${order.VatAmount:N2}").FontSize(9);
                    totals.Item().Text($"Total: ${order.Total:N2}").Bold();
                    if (order.Balance > 0)
                        totals.Item().Text($"Saldo: ${order.Balance:N2}").FontColor(Colors.Orange.Darken2);
                });
            }
        });
    }

    private static void BuildNotesSection(IContainer container, string title, string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return;
        container.Column(col =>
        {
            col.Item().Text(title).Bold().FontSize(9).FontColor(Colors.Grey.Darken2);
            col.Item().PaddingTop(2).Text(notes);
        });
    }

    private static void BuildFooter(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Text("Generado con TallerControl")
                .FontSize(8).FontColor(Colors.Grey.Darken1);
            row.RelativeItem().AlignRight()
                .Text(text =>
                {
                    text.Span("Página ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken1);
                    text.Span(" de ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    text.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken1);
                });
        });
    }

    private static string StatusLabel(WorkOrderStatus status) => status switch
    {
        WorkOrderStatus.Pending => "Pendiente",
        WorkOrderStatus.InProgress => "En proceso",
        WorkOrderStatus.WaitingParts => "Esperando repuestos",
        WorkOrderStatus.Completed => "Completada",
        WorkOrderStatus.Delivered => "Entregada",
        WorkOrderStatus.Cancelled => "Cancelada",
        _ => status.ToString(),
    };

    private static string PriorityLabel(WorkOrderPriority priority) => priority switch
    {
        WorkOrderPriority.Low => "Baja",
        WorkOrderPriority.Normal => "Normal",
        WorkOrderPriority.High => "Alta",
        _ => priority.ToString(),
    };
}
