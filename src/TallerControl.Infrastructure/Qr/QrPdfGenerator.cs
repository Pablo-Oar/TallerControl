using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TallerControl.Application.Common;
using TallerControl.Application.Qr;
using TallerControl.Domain.Qr;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Vehicles;
using TallerControl.Infrastructure.Branding;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Qr;

/// <summary>
/// Genera la tarjeta imprimible del llavero QR (REQ-17, T-DOC-04).
/// Decisión del 12/11/2026: la tarjeta lleva la patente en grande (el acceso al portal sigue exigiendo OTP o código del cliente).
/// </summary>
internal sealed class QrPdfGenerator : IQrPdfGenerator
{
    private readonly AppDbContext _db;
    private readonly QrOptions _options;
    private readonly IWorkshopBranding _branding;

    public QrPdfGenerator(AppDbContext db, IOptions<QrOptions> options, IWorkshopBranding branding)
    {
        _db = db;
        _options = options.Value;
        _branding = branding;
    }

    public async Task<byte[]> GenerateCardAsync(Guid qrCardId, CancellationToken ct)
    {
        var card = await _db.Set<QrCard>()
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(q => q.Id == qrCardId, ct)
            ?? throw new InvalidOperationException($"Tarjeta QR {qrCardId} no encontrada.");

        var shop = await _branding.GetPublicAsync(card.TenantId, ct);
        var tenantName = shop.Name;
        var contact = string.Join("  ·  ", new[] { shop.Address, shop.Phone }.Where(x => !string.IsNullOrWhiteSpace(x)));

        string? vehicleBrand = null;
        string? vehicleModel = null;
        string? vehicleVersion = null;
        int? vehicleYear = null;
        string? vehiclePlate = null;
        if (card.VehicleId.HasValue)
        {
            var v = await _db.Set<Vehicle>()
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Select(x => new { x.Id, x.Brand, x.Model, x.VersionName, x.Year, x.Plate })
                .FirstOrDefaultAsync(x => x.Id == card.VehicleId.Value, ct);
            vehicleBrand = v?.Brand;
            vehicleModel = v?.Model;
            vehicleVersion = v?.VersionName;
            vehicleYear = v?.Year;
            vehiclePlate = v?.Plate;
        }

        var url = $"{_options.BaseUrl.TrimEnd('/')}/q/{card.Code}";
        var qrBytes = GenerateQrPng(url);

        QuestPDF.Settings.License = LicenseType.Community;

        var logo = (await _branding.GetLogoAsync(card.TenantId, ct))?.Bytes;
        var description = string.Join(" ", new[] { vehicleBrand, vehicleModel, vehicleVersion, vehicleYear?.ToString() }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

        return PdfLogo.Generate(l => Build(l), logo);

        byte[] Build(byte[]? logoBytes) => Document.Create(container =>
        {
            container.Page(page =>
            {
                // Formato tarjeta tipo crédito apaisada: 85.6 × 54 mm
                page.Size(85.6f, 54f, Unit.Millimetre);
                page.Margin(0);
                page.DefaultTextStyle(t => t.FontSize(7).FontFamily("Arial").FontColor(PdfStyle.Ink));

                // Banda azul marino con el nombre del taller; el resto de la tarjeta es blanco.
                page.Header().Background(PdfStyle.Navy).PaddingVertical(2.4f, Unit.Millimetre).PaddingHorizontal(4, Unit.Millimetre)
                    .Row(row =>
                    {
                        if (logoBytes is { Length: > 0 })
                            row.ConstantItem(9, Unit.Millimetre).PaddingRight(2, Unit.Millimetre).Height(7, Unit.Millimetre).Image(logoBytes).FitArea();
                        row.RelativeItem().AlignMiddle().Column(name =>
                        {
                            name.Item().Text(tenantName).Bold().FontSize(9).FontColor(Colors.White);
                            if (contact.Length > 0)
                                name.Item().Text(contact).FontSize(5.5f).FontColor("#cbd5e1");
                        });
                        row.AutoItem().AlignMiddle().Text("HISTORIAL DIGITAL DEL VEHÍCULO").FontSize(5).Bold().LetterSpacing(0.1f).FontColor("#a78bfa");
                    });

                page.Content().PaddingHorizontal(4, Unit.Millimetre).PaddingVertical(3, Unit.Millimetre).Row(row =>
                {
                    row.ConstantItem(29, Unit.Millimetre).Column(qr =>
                    {
                        qr.Item().Border(0.6f).BorderColor(PdfStyle.Line).Padding(1.2f, Unit.Millimetre).Image(qrBytes).FitArea();
                    });

                    row.RelativeItem().PaddingLeft(4, Unit.Millimetre).Column(info =>
                    {
                        if (!string.IsNullOrEmpty(vehiclePlate))
                            info.Item().Text(vehiclePlate).Bold().FontSize(18).LetterSpacing(0.08f).FontColor(PdfStyle.Navy);

                        if (description.Length > 0)
                            info.Item().PaddingTop(1).Text(description).FontSize(8).FontColor(PdfStyle.Muted);

                        info.Item().PaddingTop(5).BorderLeft(1.2f).BorderColor(PdfStyle.Violet).PaddingLeft(2)
                            .Text("Escaneá el código para ver el historial de tu vehículo.").FontSize(7).LineHeight(1.3f);
                    });
                });

                page.Footer().PaddingBottom(1.5f, Unit.Millimetre).AlignCenter()
                    .Text("Generado con TallerControl — www.tallercontrol.com.ar")
                    .FontSize(5).FontColor(Colors.Grey.Medium);
            });
        }).GeneratePdf();
    }

    public async Task<byte[]> GenerateFreeSheetAsync(int maxCards, CancellationToken ct)
    {
        // Las tarjetas libres del taller de la sesión (filtro global de taller activo).
        var codes = await _db.Set<QrCard>()
            .AsNoTracking()
            .Where(c => c.Status == QrCardStatus.Free)
            .OrderBy(c => c.CreatedAt)
            .Take(maxCards)
            .Select(c => c.Code)
            .ToListAsync(ct);

        if (codes.Count == 0)
            throw new BusinessRuleException("No hay tarjetas libres para imprimir.");

        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var images = codes.Select(code => (Code: code, Png: GenerateQrPng($"{baseUrl}/q/{code}"))).ToList();

        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(10, Unit.Millimetre);
                page.DefaultTextStyle(t => t.FontSize(8));
                page.Header().Text("Tarjetas QR libres: recortá y asignalas a cada llavero")
                    .FontSize(9).FontColor(Colors.Grey.Darken1);

                page.Content().PaddingTop(4, Unit.Millimetre).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        for (var i = 0; i < 4; i++) c.RelativeColumn();
                    });

                    foreach (var (code, png) in images)
                    {
                        table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Column(col =>
                        {
                            col.Item().AlignCenter().Width(30, Unit.Millimetre).Image(png);
                            col.Item().PaddingTop(1).AlignCenter().Text(code).FontFamily("Consolas").Bold().FontSize(8);
                        });
                    }
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
