using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TallerControl.Application.Common;
using TallerControl.Application.Work;
using TallerControl.Domain.Work;
using TallerControl.Infrastructure.Branding;

namespace TallerControl.Infrastructure.Work;

/// <summary>
/// Genera el PDF de una OT con QuestPDF Community (D-05, T-DOC-02, REQ-16).
/// QuestPDF Community requiere mención de la licencia en el pie: MIT, documentado.
/// </summary>
internal sealed class WorkOrderPdfGenerator : IWorkOrderPdfGenerator
{
    private readonly IWorkshopBranding _branding;
    private readonly ITenantContext _tenant;

    public WorkOrderPdfGenerator(IWorkshopBranding branding, ITenantContext tenant)
    {
        _branding = branding;
        _tenant = tenant;
    }

    public async Task<byte[]> GenerateAsync(WorkOrderDto order, CancellationToken ct)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var logo = _tenant.TenantId is { } tenantId ? (await _branding.GetLogoAsync(tenantId, ct))?.Bytes : null;
        return PdfLogo.Generate(l => Build(order, l), logo);
    }

    private static byte[] Build(WorkOrderDto order, byte[]? logo)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                PdfStyle.ApplyPage(page);

                page.Header().Element(c => PdfStyle.Header(
                    c, logo,
                    $"Orden de Trabajo #{order.Number}",
                    StatusLabel(order.Status),
                    $"Prioridad: {PriorityLabel(order.Priority)}",
                    $"Creada: {order.CreatedAt:dd/MM/yyyy}"));
                page.Content().PaddingTop(4).Element(c => BuildContent(c, order));
                page.Footer().Element(PdfStyle.Footer);
            });
        });

        return document.GeneratePdf();
    }

    private static void BuildContent(IContainer container, WorkOrderDto order)
    {
        container.Column(col =>
        {
            col.Spacing(16);

            col.Item().Element(c => BuildInfoSection(c, order));

            if (!string.IsNullOrWhiteSpace(order.ReportedIssue))
                col.Item().Element(c => BuildTextSection(c, "Problema reportado", order.ReportedIssue));

            if (order.Items.Count > 0)
                col.Item().Element(c => BuildItemsTable(c, order));

            if (!string.IsNullOrWhiteSpace(order.MechanicNotes))
                col.Item().Element(c => BuildTextSection(c, "Notas del mecánico", order.MechanicNotes));

            // Las notas internas son del taller: nunca van en el PDF que se le entrega al cliente.
        });
    }

    private static void BuildInfoSection(IContainer container, WorkOrderDto order)
    {
        container.Element(PdfStyle.InfoBox).Row(row =>
        {
            row.Spacing(12);
            PdfStyle.Field(row, "Cliente", order.CustomerName);
            PdfStyle.Field(
                row, "Vehículo", order.VehiclePlate,
                string.IsNullOrEmpty(order.VehicleBrand) ? null : $"{order.VehicleBrand} {order.VehicleModel}".Trim(),
                mono: true);
            PdfStyle.Field(
                row, "Odómetro", $"{order.OdometerKm:N0} km",
                order.FuelLevel.HasValue ? $"Combustible: {order.FuelLevel}%" : null);
            PdfStyle.Field(row, "Mecánico", order.AssignedMechanicName ?? "—");
        });
    }

    private static void BuildTextSection(IContainer container, string title, string text)
    {
        container.Column(col =>
        {
            PdfStyle.SectionTitle(col, title);
            col.Item().BorderLeft(2).BorderColor(PdfStyle.Violet).PaddingLeft(8).Text(text).LineHeight(1.35f);
        });
    }

    private static void BuildItemsTable(IContainer container, WorkOrderDto order)
    {
        container.Column(col =>
        {
            PdfStyle.SectionTitle(col, "Renglones");

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(4.5f); // descripción
                    columns.RelativeColumn(1);    // cantidad
                    columns.RelativeColumn(2);    // precio neto
                    columns.RelativeColumn(1.2f); // iva %
                    columns.RelativeColumn(2);    // total línea
                });

                table.Header(header =>
                {
                    header.Cell().Element(c => PdfStyle.HeadText(c, "Descripción"));
                    header.Cell().Element(c => PdfStyle.HeadText(c, "Cant.", true));
                    header.Cell().Element(c => PdfStyle.HeadText(c, "P. neto", true));
                    header.Cell().Element(c => PdfStyle.HeadText(c, "IVA", true));
                    header.Cell().Element(c => PdfStyle.HeadText(c, "Total", true));
                });

                var i = 0;
                foreach (var item in order.Items)
                {
                    var idx = i++;
                    table.Cell().Element(c => PdfStyle.BodyCell(c, idx)).Text(item.Description);
                    table.Cell().Element(c => PdfStyle.BodyCell(c, idx)).AlignRight().Text(item.Quantity.ToString("G29"));
                    table.Cell().Element(c => PdfStyle.BodyCell(c, idx)).AlignRight().Text(
                        order.SubtotalNet.HasValue ? $"$ {item.UnitNetPrice:N2}" : "—");
                    table.Cell().Element(c => PdfStyle.BodyCell(c, idx)).AlignRight().Text($"{item.VatRate:0.##} %");
                    table.Cell().Element(c => PdfStyle.BodyCell(c, idx)).AlignRight().Text(
                        order.Total.HasValue ? $"$ {item.LineTotal:N2}" : "—");
                }
            });

            if (order.Total.HasValue)
            {
                col.Item().PaddingTop(10).AlignRight().Width(220).Column(totals =>
                {
                    PdfStyle.TotalLine(totals, "Subtotal neto", $"$ {order.SubtotalNet:N2}");
                    PdfStyle.TotalLine(totals, "IVA", $"$ {order.VatAmount:N2}");
                    PdfStyle.GrandTotal(totals, "TOTAL", $"$ {order.Total:N2}");
                    if (order.Balance > 0)
                        PdfStyle.TotalLine(totals, "Saldo pendiente", $"$ {order.Balance:N2}", Colors.Orange.Darken2);
                });
            }
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
