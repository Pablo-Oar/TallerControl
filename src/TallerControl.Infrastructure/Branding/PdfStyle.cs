using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace TallerControl.Infrastructure.Branding;

/// <summary>Estilo común de los PDF de documentos (orden, presupuesto): colores de la marca, tablas y pie.</summary>
internal static class PdfStyle
{
    public const string Navy = "#0a1628";
    public const string Violet = "#7c3aed";
    public const string Soft = "#f1f4f8";
    public const string Line = "#c7ccd6";
    public const string Muted = "#5b6b82";
    public const string Ink = "#0f172a";

    /// <summary>Márgenes de la hoja A4: aire a los costados y arriba/abajo para el pie.</summary>
    public static void ApplyPage(PageDescriptor page)
    {
        page.Size(PageSizes.A4);
        page.MarginHorizontal(2.2f, Unit.Centimetre);
        page.MarginVertical(1.8f, Unit.Centimetre);
        page.DefaultTextStyle(t => t.FontSize(10).FontFamily("Arial").FontColor(Ink));
    }

    /// <summary>Encabezado: logo, título con sello de estado y datos a la derecha; cierra con una línea violeta.</summary>
    public static void Header(
        IContainer container, byte[]? logo, string title, string status, string rightTop, string rightBottom, string? statusColor = null)
    {
        container.PaddingBottom(14).BorderBottom(2).BorderColor(Violet).PaddingBottom(10).Row(row =>
        {
            PdfLogo.Place(row, logo, 70, 52);
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(title).Bold().FontSize(20).FontColor(Navy);
                col.Item().PaddingTop(4).AlignLeft().Background(Soft)
                    .PaddingHorizontal(8).PaddingVertical(2)
                    .Text(status).FontSize(9).SemiBold().FontColor(statusColor ?? Violet);
            });
            row.ConstantItem(150).AlignRight().Column(col =>
            {
                col.Item().AlignRight().Text(rightTop).FontSize(9).SemiBold();
                col.Item().AlignRight().PaddingTop(2).Text(rightBottom).FontSize(9).FontColor(Muted);
            });
        });
    }

    /// <summary>Título de sección: mayúsculas chicas en violeta.</summary>
    public static void SectionTitle(ColumnDescriptor col, string text) =>
        col.Item().PaddingBottom(4).Text(text.ToUpperInvariant())
            .FontSize(8).Bold().LetterSpacing(0.1f).FontColor(Violet);

    /// <summary>Dato con etiqueta chica arriba y valor abajo (para la caja de cliente / vehículo).</summary>
    public static void Field(RowDescriptor row, string label, string value, string? sub = null, bool mono = false)
    {
        row.RelativeItem().Column(col =>
        {
            col.Item().Text(label.ToUpperInvariant()).FontSize(7.5f).Bold().LetterSpacing(0.08f).FontColor(Muted);
            var text = col.Item().PaddingTop(2).Text(value).SemiBold();
            if (mono) text.FontFamily("Courier New");
            if (!string.IsNullOrWhiteSpace(sub))
                col.Item().Text(sub).FontSize(9).FontColor(Muted);
        });
    }

    /// <summary>Caja gris suave que agrupa los datos principales del documento.</summary>
    public static IContainer InfoBox(IContainer c) =>
        c.Background(Soft).Border(0.5f).BorderColor(Line).PaddingVertical(10).PaddingHorizontal(12);

    public static IContainer HeaderCell(IContainer c) =>
        c.Background(Navy).PaddingVertical(6).PaddingHorizontal(7);

    public static IContainer BodyCell(IContainer c, int index) =>
        c.Background(index % 2 == 1 ? Soft : Colors.White)
            .BorderBottom(0.5f).BorderColor(Line).PaddingVertical(6).PaddingHorizontal(7);

    public static void HeadText(IContainer c, string text, bool right = false)
    {
        var cell = HeaderCell(c);
        if (right) cell = cell.AlignRight();
        cell.Text(text).FontSize(8.5f).Bold().FontColor(Colors.White);
    }

    /// <summary>Fila de totales (etiqueta a la izquierda, importe a la derecha) dentro de la caja de totales.</summary>
    public static void TotalLine(ColumnDescriptor col, string label, string value, string? color = null)
    {
        col.Item().PaddingVertical(2).Row(r =>
        {
            r.RelativeItem().Text(label).FontSize(9).FontColor(color ?? Muted);
            r.AutoItem().Text(value).FontSize(9).FontColor(color ?? Ink);
        });
    }

    /// <summary>Total final destacado sobre azul marino.</summary>
    public static void GrandTotal(ColumnDescriptor col, string label, string value) =>
        col.Item().PaddingTop(4).Background(Navy).PaddingVertical(7).PaddingHorizontal(10).Row(r =>
        {
            r.RelativeItem().Text(label).Bold().FontSize(10).FontColor(Colors.White);
            r.AutoItem().Text(value).Bold().FontSize(13).FontColor(Colors.White);
        });

    public static void Footer(IContainer container) =>
        container.BorderTop(0.5f).BorderColor(Line).PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text("Generado con TallerControl · PDF con QuestPDF (licencia MIT)")
                .FontSize(7.5f).FontColor(Muted);
            row.AutoItem().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(Muted));
                t.Span("Página ");
                t.CurrentPageNumber();
                t.Span(" de ");
                t.TotalPages();
            });
        });
}
