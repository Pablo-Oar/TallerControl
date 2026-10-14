using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TallerControl.Application.Qr;
using TallerControl.Domain.Qr;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Vehicles;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Qr;

/// <summary>
/// Genera la tarjeta imprimible del llavero QR (REQ-17, T-DOC-04).
/// La tarjeta NO incluye la patente del vehículo (CA-3 del requisito).
/// </summary>
internal sealed class QrPdfGenerator : IQrPdfGenerator
{
    private readonly AppDbContext _db;
    private readonly QrOptions _options;

    public QrPdfGenerator(AppDbContext db, IOptions<QrOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<byte[]> GenerateCardAsync(Guid qrCardId, CancellationToken ct)
    {
        var card = await _db.Set<QrCard>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(q => q.Id == qrCardId, ct)
            ?? throw new InvalidOperationException($"Tarjeta QR {qrCardId} no encontrada.");

        var tenantName = await _db.Set<Tenant>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => t.Id == card.TenantId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(ct) ?? "Taller";

        string? vehicleBrand = null;
        string? vehicleModel = null;
        if (card.VehicleId.HasValue)
        {
            var v = await _db.Set<Vehicle>()
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Select(x => new { x.Id, x.Brand, x.Model })
                .FirstOrDefaultAsync(x => x.Id == card.VehicleId.Value, ct);
            vehicleBrand = v?.Brand;
            vehicleModel = v?.Model;
        }

        var url = $"{_options.BaseUrl.TrimEnd('/')}/q/{card.Code}";
        var qrBytes = GenerateQrPng(url);

        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                // Formato tarjeta tipo crédito: 85.6 × 54 mm
                page.Size(85.6f, 54f, Unit.Millimetre);
                page.Margin(4, Unit.Millimetre);
                page.DefaultTextStyle(t => t.FontSize(7));

                page.Content().Column(col =>
                {
                    col.Item().Text(tenantName)
                        .Bold().FontSize(9).AlignCenter();

                    col.Item().PaddingTop(2).Row(row =>
                    {
                        row.RelativeItem().Image(qrBytes);

                        row.ConstantItem(40, Unit.Millimetre).Column(info =>
                        {
                            info.Item().Text("Llavero de vehículo").Bold();

                            if (vehicleBrand is not null || vehicleModel is not null)
                                info.Item().Text($"{vehicleBrand} {vehicleModel}".Trim()).FontSize(7);

                            // La patente NO se incluye (REQ-17 CA-3, T-DOC-04)
                            info.Item().PaddingTop(4).Text("Escaneá para ver el historial de tu vehículo.")
                                .FontSize(6);
                        });
                    });

                    col.Item().PaddingTop(2)
                        .Text("Generado con TallerControl — www.tallercontrol.com.ar")
                        .FontSize(5).FontColor(Colors.Grey.Medium).AlignCenter();
                });
            });
        }).GeneratePdf();
    }

    private static byte[] GenerateQrPng(string url)
    {
        using var generator = new QRCodeGenerator();
        var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(data);
        return qrCode.GetGraphic(5);
    }
}
