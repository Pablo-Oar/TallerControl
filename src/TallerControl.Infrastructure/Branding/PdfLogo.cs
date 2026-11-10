using QuestPDF.Fluent;

namespace TallerControl.Infrastructure.Branding;

/// <summary>Logo del taller en el encabezado de los PDF.</summary>
internal static class PdfLogo
{
    /// <summary>Agrega el logo a la izquierda de la fila (si hay), ajustado a un recuadro fijo.</summary>
    public static void Place(RowDescriptor row, byte[]? logo, float width = 64, float height = 48)
    {
        if (logo is not { Length: > 0 }) return;
        row.ConstantItem(width).PaddingRight(10).Height(height).Image(logo).FitArea();
    }

    /// <summary>Genera el PDF con el logo; si la imagen no se puede dibujar, lo genera igual sin ella.</summary>
    public static byte[] Generate(Func<byte[]?, byte[]> build, byte[]? logo)
    {
        if (logo is null) return build(null);
        try
        {
            return build(logo);
        }
        catch (Exception)
        {
            return build(null);
        }
    }
}
