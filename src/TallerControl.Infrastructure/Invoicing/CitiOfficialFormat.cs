using System.Globalization;
using System.Text;
using TallerControl.Application.Invoicing;

namespace TallerControl.Infrastructure.Invoicing;

/// <summary>
/// Registros de "Compras y Ventas" (Régimen de Información, RG 3685) en el diseño de ancho fijo de ARCA (D-32):
/// ventas/compras cabecera (266 / 325 caracteres) y sus alícuotas (62 / 84 caracteres).
/// Cada campo numérico va con ceros a la izquierda y los importes en centavos sin separador (15 dígitos);
/// los textos van en mayúsculas, sin acentos, completados con espacios a la derecha.
/// </summary>
internal static class CitiOfficialFormat
{
    public const int SalesHeaderLength = 266;
    public const int SalesRatesLength = 62;
    public const int PurchasesHeaderLength = 325;
    public const int PurchasesRatesLength = 84;

    public static string SalesHeader(IvaBookEntryDto e)
    {
        var (pos, number) = SplitNumber(e.InvoiceNumber);
        var (docType, docNumber, name) = Buyer(e);
        var lines = Rates(e);
        var sb = new StringBuilder(SalesHeaderLength);

        sb.Append(e.Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture)); // 8  fecha
        sb.Append(Num(VoucherCode(e.InvoiceType, e.IsCredit), 3));            // 3  tipo de comprobante
        sb.Append(Num(pos, 5));                                               // 5  punto de venta
        sb.Append(Num(number, 20));                                           // 20 número
        sb.Append(Num(number, 20));                                           // 20 número hasta
        sb.Append(Num(docType, 2));                                           // 2  código documento comprador
        sb.Append(Num(docNumber, 20));                                        // 20 identificación comprador
        sb.Append(Text(name, 30));                                            // 30 denominación comprador
        sb.Append(Money(e.TotalAmount));                                      // 15 importe total
        sb.Append(Money(0));                                                  // 15 no integran precio neto gravado
        sb.Append(Money(0));                                                  // 15 percepción a no categorizados
        sb.Append(Money(0));                                                  // 15 operaciones exentas
        sb.Append(Money(0));                                                  // 15 percepciones impuestos nacionales
        sb.Append(Money(0));                                                  // 15 percepciones ingresos brutos
        sb.Append(Money(0));                                                  // 15 percepciones impuestos municipales
        sb.Append(Money(0));                                                  // 15 impuestos internos
        sb.Append("PES");                                                     // 3  moneda
        sb.Append("0001000000");                                              // 10 tipo de cambio
        sb.Append(Math.Min(9, Math.Max(1, lines.Count)));                     // 1  cantidad de alícuotas
        sb.Append(' ');                                                       // 1  código de operación
        sb.Append(Money(0));                                                  // 15 otros tributos
        sb.Append(e.Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture)); // 8  vencimiento de pago

        return sb.ToString();
    }

    public static IEnumerable<string> SalesRates(IvaBookEntryDto e)
    {
        var (pos, number) = SplitNumber(e.InvoiceNumber);
        foreach (var line in Rates(e))
        {
            yield return Num(VoucherCode(e.InvoiceType, e.IsCredit), 3)
                + Num(pos, 5)
                + Num(number, 20)
                + Money(line.Net)
                + Num(RateCode(line.VatRate), 4)
                + Money(line.Vat);
        }
    }

    public static string PurchasesHeader(IvaBookEntryDto e)
    {
        var (pos, number) = SplitNumber(e.InvoiceNumber);
        var lines = Rates(e);
        var net = lines.Sum(l => Math.Abs(l.Net));
        var vat = lines.Sum(l => Math.Abs(l.Vat));
        // Lo que no es neto, IVA ni percepciones (p. ej. un redondeo o un concepto sin IVA) va como «no gravado»
        // para que el total del comprobante siempre cierre.
        var notTaxed = Math.Max(0m, Math.Abs(e.TotalAmount) - net - vat - e.Perceptions);
        var sb = new StringBuilder(PurchasesHeaderLength);

        sb.Append(e.Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture)); // 8  fecha
        sb.Append(Num(VoucherCode(e.InvoiceType, e.IsCredit), 3));            // 3  tipo de comprobante
        sb.Append(Num(pos, 5));                                               // 5  punto de venta
        sb.Append(Num(number, 20));                                           // 20 número
        sb.Append(new string(' ', 16));                                       // 16 despacho de importación
        sb.Append(Num(SellerDocType(e), 2));                                  // 2  código documento vendedor
        sb.Append(Num(Digits(e.CounterpartyCuit), 20));                       // 20 identificación vendedor
        sb.Append(Text(e.CounterpartyName, 30));                              // 30 denominación vendedor
        sb.Append(Money(e.TotalAmount));                                      // 15 importe total
        sb.Append(Money(notTaxed));                                           // 15 no integran precio neto gravado
        sb.Append(Money(0));                                                  // 15 operaciones exentas
        sb.Append(Money(0));                                                  // 15 percepciones o pagos a cuenta de IVA
        sb.Append(Money(0));                                                  // 15 percepciones otros impuestos nacionales
        sb.Append(Money(0));                                                  // 15 percepciones ingresos brutos
        sb.Append(Money(0));                                                  // 15 percepciones impuestos municipales
        sb.Append(Money(0));                                                  // 15 impuestos internos
        sb.Append("PES");                                                     // 3  moneda
        sb.Append("0001000000");                                              // 10 tipo de cambio
        sb.Append(Math.Min(9, Math.Max(1, lines.Count)));                     // 1  cantidad de alícuotas
        sb.Append('0');                                                       // 1  código de operación
        sb.Append(Money(vat));                                                // 15 crédito fiscal computable
        sb.Append(Money(e.Perceptions));                                      // 15 otros tributos
        sb.Append(new string('0', 11));                                       // 11 CUIT emisor / corredor
        sb.Append(new string(' ', 30));                                       // 30 denominación emisor / corredor
        sb.Append(Money(0));                                                  // 15 IVA comisión

        return sb.ToString();
    }

    public static IEnumerable<string> PurchasesRates(IvaBookEntryDto e)
    {
        var (pos, number) = SplitNumber(e.InvoiceNumber);
        foreach (var line in Rates(e))
        {
            yield return Num(VoucherCode(e.InvoiceType, e.IsCredit), 3)
                + Num(pos, 5)
                + Num(number, 20)
                + Num(SellerDocType(e), 2)
                + Num(Digits(e.CounterpartyCuit), 20)
                + Money(line.Net)
                + Num(RateCode(line.VatRate), 4)
                + Money(line.Vat);
        }
    }

    // ── Piezas ────────────────────────────────────────────────────────────────

    /// <summary>Código de comprobante de ARCA (tabla de comprobantes): A 1/3, B 6/8, C 11/13, M 51/53 (factura / nota de crédito).</summary>
    internal static long VoucherCode(string? letter, bool isCredit) =>
        (letter?.Trim().ToUpperInvariant(), isCredit) switch
        {
            ("A", false) => 1,
            ("A", true) => 3,
            ("B", false) => 6,
            ("B", true) => 8,
            ("M", false) => 51,
            ("M", true) => 53,
            (_, true) => 13,
            _ => 11,
        };

    /// <summary>Código de alícuota de IVA: 0% = 3, 10,5% = 4, 21% = 5, 27% = 6, 5% = 8, 2,5% = 9.</summary>
    internal static long RateCode(decimal vatRatePercent)
    {
        var r = Math.Round(vatRatePercent, 2);
        return r switch
        {
            10.5m => 4,
            21m => 5,
            27m => 6,
            5m => 8,
            2.5m => 9,
            _ => 3,
        };
    }

    /// <summary>Alícuotas del comprobante: las del detalle si se conocen; si no, una sola deducida de IVA / neto.</summary>
    private static IReadOnlyList<IvaRateLineDto> Rates(IvaBookEntryDto e)
    {
        if (e.RateLines is { Count: > 0 })
            return e.RateLines.Select(l => l with { Net = Math.Abs(l.Net), Vat = Math.Abs(l.Vat) }).ToList();

        var net = Math.Abs(e.NetAmount);
        var vat = Math.Abs(e.VatAmount);
        var rate = net == 0 ? 0m : Math.Round(vat / net * 100m, 1, MidpointRounding.AwayFromZero);
        return [new IvaRateLineDto(rate, net, vat)];
    }

    private static (long Pos, long Number) SplitNumber(string? invoiceNumber)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber)) return (0, 0);
        var parts = invoiceNumber.Split('-');
        return parts.Length == 2
               && long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var pos)
               && long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? (pos, number)
            : (0, 0);
    }

    private static (long DocType, long DocNumber, string Name) Buyer(IvaBookEntryDto e)
    {
        var digits = Digits(e.CounterpartyCuit);
        // Sin documento: «99» (venta sin identificar) con el número en cero.
        if (digits == 0) return (99, 0, "CONSUMIDOR FINAL");
        return (BuyerDocType(e.CounterpartyDocType), digits, e.CounterpartyName);
    }

    // Tabla de documentos de ARCA: CUIT 80, CUIL 86, DNI 96, pasaporte 94, otro 99.
    private static long BuyerDocType(string? docType) =>
        docType?.ToLowerInvariant() switch
        {
            "cuit" => 80,
            "cuil" => 86,
            "dni" => 96,
            "pasaporte" => 94,
            _ => 99,
        };

    private static long SellerDocType(IvaBookEntryDto e) => Digits(e.CounterpartyCuit) == 0 ? 99 : 80;

    private static long Digits(string? value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        var d = new string(value.Where(char.IsDigit).ToArray());
        return d.Length is > 0 and <= 18 && long.TryParse(d, out var n) ? n : 0;
    }

    private static string Num(long value, int width) =>
        Math.Abs(value).ToString(CultureInfo.InvariantCulture).PadLeft(width, '0');

    /// <summary>Importe en centavos, 15 dígitos, en valor absoluto (el signo lo da el tipo de comprobante).</summary>
    private static string Money(decimal amount) =>
        ((long)Math.Round(Math.Abs(amount) * 100m, 0, MidpointRounding.AwayFromZero))
            .ToString(CultureInfo.InvariantCulture).PadLeft(15, '0');

    private static string Text(string? value, int width)
    {
        var ascii = new StringBuilder();
        foreach (var ch in (value ?? string.Empty).Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            ascii.Append(ch is >= ' ' and <= '~' ? char.ToUpperInvariant(ch) : ' ');
        }

        var s = ascii.ToString().Trim();
        return s.Length >= width ? s[..width] : s.PadRight(width);
    }
}
