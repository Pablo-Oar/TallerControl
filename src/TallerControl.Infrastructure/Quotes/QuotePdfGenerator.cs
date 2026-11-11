using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TallerControl.Application.Common;
using TallerControl.Application.PublicLinks;
using TallerControl.Application.Quotes;
using TallerControl.Domain.Quotes;
using TallerControl.Infrastructure.Branding;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Quotes;

/// <summary>
/// Genera el PDF de un presupuesto con QuestPDF Community (D-05, T-DOC-01, REQ-16).
/// QuestPDF Community requiere mención de la licencia en el pie: MIT, documentado.
/// </summary>
internal sealed class QuotePdfGenerator : IQuotePdfGenerator
{
    private const long MaxSignatureBytes = 2 * 1024 * 1024;

    private readonly IWorkshopBranding _branding;
    private readonly ITenantContext _tenant;
    private readonly AppDbContext _db;
    private readonly IObjectStorage _storage;
    private readonly ISignatureEncryptor _encryptor;

    public QuotePdfGenerator(
        IWorkshopBranding branding, ITenantContext tenant, AppDbContext db, IObjectStorage storage, ISignatureEncryptor encryptor)
    {
        _branding = branding;
        _tenant = tenant;
        _db = db;
        _storage = storage;
        _encryptor = encryptor;
    }

    /// <summary>Quién aprobó el presupuesto: nombre, documento y firma (si dejó). Datos del cliente, nunca del taller.</summary>
    private sealed record Acceptance(string SignerName, string? DocNumber, byte[]? Signature, DateTimeOffset At, bool Partial);

    public async Task<byte[]> GenerateAsync(QuoteDto quote, CancellationToken ct)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var logo = _tenant.TenantId is { } tenantId ? (await _branding.GetLogoAsync(tenantId, ct))?.Bytes : null;
        var acceptance = await LoadAcceptanceAsync(quote, ct);
        return PdfLogo.Generate(l => Build(quote, l, acceptance), logo);
    }

    /// <summary>
    /// Si el presupuesto ya fue aprobado, toma la respuesta vigente del cliente con su firma y documento.
    /// Nunca hace fallar el PDF: lo que no se pueda leer o descifrar simplemente no se muestra.
    /// </summary>
    private async Task<Acceptance?> LoadAcceptanceAsync(QuoteDto quote, CancellationToken ct)
    {
        if (quote.Status is not (QuoteStatus.Approved or QuoteStatus.PartiallyApproved or QuoteStatus.Converted))
            return null;

        try
        {
            var r = await _db.Set<QuoteResponse>()
                .AsNoTracking()
                .Where(x => x.QuoteId == quote.Id && x.IsCurrent && x.Decision == Quote.DecisionApproved)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(ct);
            if (r is null) return null;

            string? doc = null;
            if (r.SignerDocNumberEnc is { Length: > 0 })
            {
                try { doc = _encryptor.Decrypt(r.SignerDocNumberEnc); }
                catch (Exception) { doc = null; }
            }

            byte[]? signature = null;
            if (!string.IsNullOrWhiteSpace(r.SignatureFileKey))
            {
                signature = await _storage.ReadAllAsync(r.SignatureFileKey, MaxSignatureBytes, ct);
                if (signature is not null)
                {
                    try { QuestPDF.Infrastructure.Image.FromBinaryData(signature); }
                    catch (Exception) { signature = null; }
                }
            }

            return new Acceptance(r.SignerName, doc, signature, r.CreatedAt, quote.Status == QuoteStatus.PartiallyApproved);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static byte[] Build(QuoteDto quote, byte[]? logo, Acceptance? acceptance)
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
                page.Content().PaddingTop(4).Element(c => BuildContent(c, quote, acceptance));
                page.Footer().Element(PdfStyle.Footer);
            });
        });

        return document.GeneratePdf();
    }

    private static void BuildContent(IContainer container, QuoteDto quote, Acceptance? acceptance)
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

            if (acceptance is not null)
                col.Item().ShowEntire().Element(c => BuildAcceptance(c, acceptance));
        });
    }

    private static void BuildAcceptance(IContainer container, Acceptance a)
    {
        container.Column(col =>
        {
            PdfStyle.SectionTitle(col, a.Partial ? "Conformidad del cliente (aprobación parcial)" : "Conformidad del cliente");

            col.Item().Border(0.5f).BorderColor(PdfStyle.Line).Padding(12).Row(row =>
            {
                row.Spacing(16);

                row.RelativeItem(1.3f).Column(sig =>
                {
                    sig.Item().Height(70).AlignMiddle().AlignCenter().Element(c =>
                    {
                        if (a.Signature is { Length: > 0 })
                            c.Image(a.Signature).FitArea();
                    });
                    sig.Item().BorderTop(0.8f).BorderColor(PdfStyle.Ink).PaddingTop(3).AlignCenter()
                        .Text("Firma del cliente").FontSize(8).FontColor(PdfStyle.Muted);
                });

                row.RelativeItem(1).Column(info =>
                {
                    info.Spacing(6);
                    info.Item().Column(f =>
                    {
                        f.Item().Text("NOMBRE").FontSize(7.5f).Bold().LetterSpacing(0.08f).FontColor(PdfStyle.Muted);
                        f.Item().PaddingTop(1).Text(a.SignerName).SemiBold();
                    });
                    if (!string.IsNullOrWhiteSpace(a.DocNumber))
                    {
                        info.Item().Column(f =>
                        {
                            f.Item().Text("DOCUMENTO").FontSize(7.5f).Bold().LetterSpacing(0.08f).FontColor(PdfStyle.Muted);
                            f.Item().PaddingTop(1).Text(a.DocNumber).SemiBold();
                        });
                    }

                    info.Item().Column(f =>
                    {
                        f.Item().Text("FECHA DE APROBACIÓN").FontSize(7.5f).Bold().LetterSpacing(0.08f).FontColor(PdfStyle.Muted);
                        f.Item().PaddingTop(1).Text($"{a.At:dd/MM/yyyy HH:mm}").SemiBold();
                    });
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
