using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TallerControl.Application.Payments;
using TallerControl.Domain.Customers;
using TallerControl.Domain.Payments;
using TallerControl.Domain.Vehicles;
using TallerControl.Domain.Work;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Payments;

/// <summary>
/// Genera el comprobante de pago con QuestPDF Community (D-05, T-DOC-03, REQ-16).
/// </summary>
internal sealed class PaymentPdfGenerator : IPaymentPdfGenerator
{
    private readonly AppDbContext _db;

    public PaymentPdfGenerator(AppDbContext db)
    {
        _db = db;
    }

    public async Task<byte[]> GenerateAsync(Guid paymentId, CancellationToken ct)
    {
        var data = await (
            from p in _db.Set<Payment>().AsNoTracking()
            join wo in _db.Set<WorkOrder>().AsNoTracking() on p.WorkOrderId equals wo.Id
            join c in _db.Set<Customer>().AsNoTracking().IgnoreQueryFilters() on wo.CustomerId equals c.Id
            join v in _db.Set<Vehicle>().AsNoTracking().IgnoreQueryFilters() on wo.VehicleId equals v.Id
            where p.Id == paymentId
            select new
            {
                Payment = p,
                WorkOrderNumber = wo.Number,
                WorkOrderTotal = wo.Total,
                CustomerName = c.Name,
                VehiclePlate = v.Plate,
                VehicleBrand = v.Brand,
                VehicleModel = v.Model,
            }
        ).FirstOrDefaultAsync(ct) ?? throw new InvalidOperationException($"Pago {paymentId} no encontrado.");

        QuestPDF.Settings.License = LicenseType.Community;

        var payment = data.Payment;
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(10).FontFamily("Arial"));

                page.Header().Element(c => BuildHeader(c, payment));
                page.Content().Element(c => BuildContent(c, payment, data.WorkOrderNumber, data.WorkOrderTotal,
                    data.CustomerName, data.VehiclePlate, data.VehicleBrand, data.VehicleModel));
                page.Footer().Element(BuildFooter);
            });
        });

        return document.GeneratePdf();
    }

    private static void BuildHeader(IContainer container, Payment payment)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text($"Comprobante de Pago #{payment.Number:D4}")
                    .Bold().FontSize(16);
                col.Item().Text(payment.Status == PaymentStatus.Voided ? "ANULADO" : "Válido")
                    .FontSize(10)
                    .FontColor(payment.Status == PaymentStatus.Voided
                        ? Colors.Red.Darken2
                        : Colors.Green.Darken2);
            });

            row.ConstantItem(180).Column(col =>
            {
                col.Item().AlignRight().Text($"Fecha: {payment.PaidAt:dd/MM/yyyy HH:mm}")
                    .FontSize(9);
                col.Item().AlignRight().Text($"Método: {MethodLabel(payment.Method)}")
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        });
    }

    private static void BuildContent(
        IContainer container,
        Payment payment,
        int workOrderNumber,
        decimal workOrderTotal,
        string customerName,
        string vehiclePlate,
        string? vehicleBrand,
        string? vehicleModel)
    {
        container.Column(col =>
        {
            col.Spacing(10);

            // Info OT / cliente / vehículo
            col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Orden de Trabajo").Bold().FontSize(9).FontColor(Colors.Grey.Darken2);
                    c.Item().Text($"#{workOrderNumber}");
                });

                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Cliente").Bold().FontSize(9).FontColor(Colors.Grey.Darken2);
                    c.Item().Text(customerName);
                });

                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Vehículo").Bold().FontSize(9).FontColor(Colors.Grey.Darken2);
                    c.Item().Text(vehiclePlate).FontFamily("Courier New");
                    if (!string.IsNullOrEmpty(vehicleBrand))
                        c.Item().Text($"{vehicleBrand} {vehicleModel}".Trim())
                            .FontColor(Colors.Grey.Darken1).FontSize(9);
                });
            });

            // Detalle del pago
            col.Item().Column(c =>
            {
                c.Item().Text("Detalle del Pago").Bold().FontSize(9).FontColor(Colors.Grey.Darken2);

                c.Item().PaddingTop(4).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(2);
                    });

                    table.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Concepto").Bold().FontSize(9);
                    table.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Importe").Bold().FontSize(9);

                    table.Cell().Padding(4).Text("Total de la orden");
                    table.Cell().Padding(4).AlignRight().Text($"${workOrderTotal:N2}");

                    table.Cell().Padding(4).Text($"Pago ({MethodLabel(payment.Method)})").Bold();
                    table.Cell().Padding(4).AlignRight().Text($"${payment.Amount:N2}").Bold();
                });
            });

            // Referencia
            if (!string.IsNullOrWhiteSpace(payment.Reference))
            {
                col.Item().Column(c =>
                {
                    c.Item().Text("Referencia").Bold().FontSize(9).FontColor(Colors.Grey.Darken2);
                    c.Item().PaddingTop(2).Text(payment.Reference);
                });
            }

            // Anulación
            if (payment.Status == PaymentStatus.Voided)
            {
                col.Item().Background(Colors.Red.Lighten4).Padding(8).Column(c =>
                {
                    c.Item().Text("PAGO ANULADO").Bold().FontColor(Colors.Red.Darken2);
                    c.Item().Text($"Motivo: {payment.VoidReason}").FontSize(9);
                    if (payment.VoidedAt.HasValue)
                        c.Item().Text($"Anulado el: {payment.VoidedAt:dd/MM/yyyy HH:mm}").FontSize(9);
                });
            }
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

    private static string MethodLabel(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Efectivo",
        PaymentMethod.Transfer => "Transferencia",
        PaymentMethod.Debit => "Débito",
        PaymentMethod.Credit => "Crédito",
        _ => method.ToString(),
    };
}
