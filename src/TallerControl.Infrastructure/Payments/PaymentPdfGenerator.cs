using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TallerControl.Application.Common;
using TallerControl.Application.Payments;
using TallerControl.Domain.Customers;
using TallerControl.Domain.Payments;
using TallerControl.Domain.Vehicles;
using TallerControl.Domain.Work;
using TallerControl.Infrastructure.Branding;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Payments;

/// <summary>
/// Genera el comprobante de pago con QuestPDF Community (D-05, T-DOC-03, REQ-16).
/// </summary>
internal sealed class PaymentPdfGenerator : IPaymentPdfGenerator
{
    private readonly AppDbContext _db;
    private readonly IWorkshopBranding _branding;

    public PaymentPdfGenerator(AppDbContext db, IWorkshopBranding branding)
    {
        _db = db;
        _branding = branding;
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
                WorkOrderPaid = wo.PaidAmount,
                CustomerName = c.Name,
                VehiclePlate = v.Plate,
                VehicleBrand = v.Brand,
                VehicleModel = v.Model,
            }
        ).FirstOrDefaultAsync(ct) ?? throw new InvalidOperationException($"Pago {paymentId} no encontrado.");

        QuestPDF.Settings.License = LicenseType.Community;

        var payment = data.Payment;
        var logo = (await _branding.GetLogoAsync(payment.TenantId, ct))?.Bytes;
        return PdfLogo.Generate(l => Build(payment, data.WorkOrderNumber, data.WorkOrderTotal, data.WorkOrderPaid, data.CustomerName,
            data.VehiclePlate, data.VehicleBrand, data.VehicleModel, l), logo);
    }

    private static byte[] Build(Payment payment, int workOrderNumber, decimal workOrderTotal, decimal workOrderPaid, string customerName,
        string vehiclePlate, string? vehicleBrand, string? vehicleModel, byte[]? logo)
    {
        var voided = payment.Status == PaymentStatus.Voided;
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                PdfStyle.ApplyPage(page);

                page.Header().Element(c => PdfStyle.Header(
                    c, logo,
                    $"Comprobante de Pago #{payment.Number:D4}",
                    voided ? "ANULADO" : "VÁLIDO",
                    $"Fecha: {payment.PaidAt:dd/MM/yyyy HH:mm}",
                    $"Método: {MethodLabel(payment.Method)}",
                    voided ? Colors.Red.Darken2 : Colors.Green.Darken2));
                page.Content().PaddingTop(4).Element(c => BuildContent(c, payment, workOrderNumber, workOrderTotal, workOrderPaid,
                    customerName, vehiclePlate, vehicleBrand, vehicleModel));
                page.Footer().Element(PdfStyle.Footer);
            });
        });

        return document.GeneratePdf();
    }

    private static void BuildContent(
        IContainer container,
        Payment payment,
        int workOrderNumber,
        decimal workOrderTotal,
        decimal workOrderPaid,
        string customerName,
        string vehiclePlate,
        string? vehicleBrand,
        string? vehicleModel)
    {
        var voided = payment.Status == PaymentStatus.Voided;

        container.Column(col =>
        {
            col.Spacing(16);

            // Orden / cliente / vehículo
            col.Item().Element(PdfStyle.InfoBox).Row(row =>
            {
                row.Spacing(12);
                PdfStyle.Field(row, "Orden de trabajo", $"#{workOrderNumber}");
                PdfStyle.Field(row, "Cliente", customerName);
                PdfStyle.Field(
                    row, "Vehículo", vehiclePlate,
                    string.IsNullOrEmpty(vehicleBrand) ? null : $"{vehicleBrand} {vehicleModel}".Trim(),
                    mono: true);
            });

            // Importe cobrado, bien a la vista
            col.Item().Background(voided ? Colors.Red.Lighten5 : PdfStyle.Soft)
                .BorderLeft(4).BorderColor(voided ? Colors.Red.Darken2 : PdfStyle.Violet)
                .PaddingVertical(12).PaddingHorizontal(14).Row(r =>
                {
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().Text(voided ? "IMPORTE ANULADO" : "IMPORTE COBRADO")
                            .FontSize(8).Bold().LetterSpacing(0.1f).FontColor(PdfStyle.Muted);
                        c.Item().PaddingTop(2).Text(MethodLabel(payment.Method)).FontSize(10).FontColor(PdfStyle.Muted);
                    });
                    r.AutoItem().AlignMiddle().Text($"$ {payment.Amount:N2}")
                        .Bold().FontSize(22).FontColor(voided ? Colors.Red.Darken2 : PdfStyle.Navy);
                });

            // Detalle del pago
            col.Item().Column(c =>
            {
                PdfStyle.SectionTitle(c, "Detalle del pago");

                c.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(x => PdfStyle.HeadText(x, "Concepto"));
                        header.Cell().Element(x => PdfStyle.HeadText(x, "Fecha de cobro", true));
                        header.Cell().Element(x => PdfStyle.HeadText(x, "Importe", true));
                    });

                    table.Cell().Element(x => PdfStyle.BodyCell(x, 0)).Text("Total de la orden");
                    table.Cell().Element(x => PdfStyle.BodyCell(x, 0)).AlignRight().Text("—").FontColor(PdfStyle.Muted);
                    table.Cell().Element(x => PdfStyle.BodyCell(x, 0)).AlignRight().Text($"$ {workOrderTotal:N2}");

                    table.Cell().Element(x => PdfStyle.BodyCell(x, 1)).Text($"Pago ({MethodLabel(payment.Method)})").SemiBold();
                    table.Cell().Element(x => PdfStyle.BodyCell(x, 1)).AlignRight().Text($"{payment.PaidAt:dd/MM/yyyy HH:mm}");
                    table.Cell().Element(x => PdfStyle.BodyCell(x, 1)).AlignRight().Text($"$ {payment.Amount:N2}").SemiBold();

                    var pending = Math.Max(0, workOrderTotal - workOrderPaid);
                    var color = pending > 0 ? Colors.Orange.Darken2 : Colors.Green.Darken2;
                    table.Cell().Element(x => PdfStyle.BodyCell(x, 0)).Text(pending > 0 ? "Pendiente de abonar" : "Orden totalmente abonada")
                        .SemiBold().FontColor(color);
                    table.Cell().Element(x => PdfStyle.BodyCell(x, 0)).AlignRight().Text("");
                    table.Cell().Element(x => PdfStyle.BodyCell(x, 0)).AlignRight().Text($"$ {pending:N2}").SemiBold().FontColor(color);
                });
            });

            // Referencia
            if (!string.IsNullOrWhiteSpace(payment.Reference))
            {
                col.Item().Column(c =>
                {
                    PdfStyle.SectionTitle(c, "Referencia");
                    c.Item().BorderLeft(2).BorderColor(PdfStyle.Violet).PaddingLeft(8).Text(payment.Reference);
                });
            }

            // Anulación
            if (voided)
            {
                col.Item().Background(Colors.Red.Lighten5).Border(0.5f).BorderColor(Colors.Red.Lighten2)
                    .Padding(10).Column(c =>
                    {
                        c.Item().Text("PAGO ANULADO").Bold().FontColor(Colors.Red.Darken2);
                        c.Item().PaddingTop(2).Text($"Motivo: {payment.VoidReason}").FontSize(9);
                        if (payment.VoidedAt.HasValue)
                            c.Item().Text($"Anulado el: {payment.VoidedAt:dd/MM/yyyy HH:mm}").FontSize(9).FontColor(PdfStyle.Muted);
                    });
            }
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
