using TallerControl.Application.Invoicing;
using TallerControl.Infrastructure.Invoicing;

namespace TallerControl.Api.IntegrationTests.Invoicing;

/// <summary>D-32: los registros CITI respetan el ancho fijo y los códigos del diseño oficial de ARCA.</summary>
public sealed class CitiOfficialFormatTests
{
    private static readonly DateTimeOffset Day = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    private static IvaBookEntryDto Sale(bool credit = false, string? doc = "20-12345678-6") =>
        new(Day, "0003-00000012", doc, "Pérez, José Ñandú", 1000m, 210m, 1210m, null,
            InvoiceType: "A", Cae: "70417054367466", IsCredit: credit, CounterpartyDocType: "Cuit");

    [Fact(DisplayName = "T-CITI-01: Los registros de ventas miden 266 y 62 caracteres")]
    public void Ventas_respetan_el_ancho_fijo()
    {
        var header = CitiOfficialFormat.SalesHeader(Sale());
        Assert.Equal(CitiOfficialFormat.SalesHeaderLength, header.Length);
        Assert.StartsWith("20261005" + "001" + "00003" + "00000000000000000012", header);
        Assert.Contains("PEREZ, JOSE NANDU", header);
        Assert.Equal("000000000121000", header.Substring(8 + 3 + 5 + 20 + 20 + 2 + 20 + 30, 15));

        var rate = Assert.Single(CitiOfficialFormat.SalesRates(Sale()));
        Assert.Equal(CitiOfficialFormat.SalesRatesLength, rate.Length);
        Assert.EndsWith("0005" + "000000000021000", rate); // 21 % → código 5, IVA 210,00
    }

    [Fact(DisplayName = "T-CITI-02: Una nota de crédito usa su código de comprobante y va en valor absoluto")]
    public void Nota_de_credito_va_en_valor_absoluto()
    {
        var credit = Sale(credit: true) with { NetAmount = -1000m, VatAmount = -210m, TotalAmount = -1210m };
        var header = CitiOfficialFormat.SalesHeader(credit);
        Assert.Equal("003", header.Substring(8, 3));
        Assert.DoesNotContain("-", header);
    }

    [Fact(DisplayName = "T-CITI-03: Sin documento del comprador se informa como consumidor final (código 99)")]
    public void Sin_documento_es_consumidor_final()
    {
        var header = CitiOfficialFormat.SalesHeader(Sale(doc: null));
        Assert.Equal("99", header.Substring(8 + 3 + 5 + 20 + 20, 2));
        Assert.Contains("CONSUMIDOR FINAL", header);
    }

    [Fact(DisplayName = "T-CITI-04: Los registros de compras miden 325 y 84 caracteres y reparten las alícuotas")]
    public void Compras_respetan_el_ancho_fijo()
    {
        var purchase = new IvaBookEntryDto(Day, "0010-00004567", "30-71234567-1", "Repuestos SA", 1500m, 220.5m, 1720.5m, null,
            InvoiceType: "A", RateLines: [new IvaRateLineDto(21m, 1000m, 210m), new IvaRateLineDto(10.5m, 500m, 10.5m)]);

        var header = CitiOfficialFormat.PurchasesHeader(purchase);
        Assert.Equal(CitiOfficialFormat.PurchasesHeaderLength, header.Length);

        var rates = CitiOfficialFormat.PurchasesRates(purchase).ToList();
        Assert.Equal(2, rates.Count);
        Assert.All(rates, r => Assert.Equal(CitiOfficialFormat.PurchasesRatesLength, r.Length));
        Assert.Contains(rates, r => r.Contains("0005"));
        Assert.Contains(rates, r => r.Contains("0004"));
    }
}
