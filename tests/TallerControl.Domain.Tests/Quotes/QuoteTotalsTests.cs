using TallerControl.Domain.Quotes;

namespace TallerControl.Domain.Tests.Quotes;

/// <summary>
/// T-QUO-02: totales del presupuesto sin descuento (SubtotalNet, VatAmount, Total).
/// T-QUO-03: descuento global aplicado correctamente sobre SubtotalNet y VatAmount.
/// REQ-10, doc 04 §3.5.
/// </summary>
public class QuoteTotalsTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly Guid CustomerId = Guid.CreateVersion7();
    private static readonly Guid VehicleId = Guid.CreateVersion7();

    private static Quote BuildQuote() => new(
        TenantId, number: 1, CustomerId, VehicleId,
        validUntil: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
        customerNotes: null, internalNotes: null, createdBy: null);

    // ── T-QUO-02: totales sin descuento ──────────────────────────────────────

    [Fact(DisplayName = "T-QUO-02: SubtotalNet = suma de lineNet de los ítems")]
    public void SubtotalNet_es_suma_de_line_nets()
    {
        var quote = BuildQuote();
        quote.AddItem(QuoteItemKind.Service, "Servicio A", null, 2m, 100m, 21m, null);
        quote.AddItem(QuoteItemKind.Service, "Servicio B", null, 3m, 50m, 10m, null);

        // LineNet A = 2 × 100 = 200; LineNet B = 3 × 50 = 150; Subtotal = 350
        Assert.Equal(350m, quote.SubtotalNet);
    }

    [Fact(DisplayName = "T-QUO-02: Total = SubtotalNet + VatAmount cuando GlobalDiscountPct = 0")]
    public void Total_igual_subtotal_mas_iva_sin_descuento()
    {
        var quote = BuildQuote();
        quote.AddItem(QuoteItemKind.Service, "Servicio", null, 1m, 1000m, 21m, null);

        // SubtotalNet = 1000; VatAmount = 210 (1000 × 21%); Total = 1210
        Assert.Equal(1000m, quote.SubtotalNet);
        Assert.Equal(0m, quote.DiscountAmount);
        Assert.Equal(210m, quote.VatAmount);
        Assert.Equal(1210m, quote.Total);
    }

    [Fact(DisplayName = "T-QUO-02: presupuesto vacío tiene todos los totales en cero")]
    public void Presupuesto_vacio_tiene_totales_cero()
    {
        var quote = BuildQuote();
        Assert.Equal(0m, quote.SubtotalNet);
        Assert.Equal(0m, quote.DiscountAmount);
        Assert.Equal(0m, quote.VatAmount);
        Assert.Equal(0m, quote.Total);
    }

    // ── T-QUO-03: descuento global ────────────────────────────────────────────

    [Fact(DisplayName = "T-QUO-03: con GlobalDiscountPct=10, DiscountAmount = 10% del SubtotalNet")]
    public void Descuento_global_calcula_discount_amount_correctamente()
    {
        var quote = BuildQuote();
        quote.Update(
            CustomerId, VehicleId,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            globalDiscountPct: 10m,
            customerNotes: null, internalNotes: null,
            items: Array.Empty<QuoteItem>());

        quote.AddItem(QuoteItemKind.Service, "Servicio", null, 1m, 1000m, 21m, null);

        // SubtotalNet = 1000; DiscountAmount = 100 (10%); base IVA = 900; VatAmount = 189; Total = 1089
        Assert.Equal(1000m, quote.SubtotalNet);
        Assert.Equal(100m, quote.DiscountAmount);
        Assert.Equal(189m, quote.VatAmount);
        Assert.Equal(1089m, quote.Total);
    }

    [Fact(DisplayName = "T-QUO-03: descuento fuera de rango (101%) lanza ArgumentOutOfRangeException")]
    public void Descuento_fuera_de_rango_lanza_excepcion()
    {
        var quote = BuildQuote();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            quote.Update(CustomerId, VehicleId,
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
                globalDiscountPct: 101m,
                customerNotes: null, internalNotes: null, items: Array.Empty<QuoteItem>()));
    }

    [Fact(DisplayName = "T-QUO-03: descuento negativo lanza ArgumentOutOfRangeException")]
    public void Descuento_negativo_lanza_excepcion()
    {
        var quote = BuildQuote();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            quote.Update(CustomerId, VehicleId,
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
                globalDiscountPct: -1m,
                customerNotes: null, internalNotes: null, items: Array.Empty<QuoteItem>()));
    }

    [Fact(DisplayName = "T-QUO-03: descuento 100% resulta en Total = 0")]
    public void Descuento_cien_porciento_total_cero()
    {
        var quote = BuildQuote();
        quote.Update(
            CustomerId, VehicleId,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            globalDiscountPct: 100m,
            customerNotes: null, internalNotes: null,
            items: Array.Empty<QuoteItem>());

        quote.AddItem(QuoteItemKind.Service, "Servicio", null, 1m, 1000m, 21m, null);

        Assert.Equal(1000m, quote.DiscountAmount);
        Assert.Equal(0m, quote.VatAmount);
        Assert.Equal(0m, quote.Total);
    }

    // ── Estado y ciclo de vida ────────────────────────────────────────────────

    [Fact(DisplayName = "T-QUO-02: Send sin ítems lanza InvalidOperationException")]
    public void Send_sin_items_lanza_excepcion()
    {
        var quote = BuildQuote();
        Assert.Throws<InvalidOperationException>(() => quote.Send(DateTimeOffset.UtcNow));
    }

    [Fact(DisplayName = "T-QUO-02: Send con ítems transiciona a Sent")]
    public void Send_con_items_transiciona_a_sent()
    {
        var quote = BuildQuote();
        quote.AddItem(QuoteItemKind.Service, "Servicio", null, 1m, 100m, 21m, null);
        quote.Send(DateTimeOffset.UtcNow);
        Assert.Equal(QuoteStatus.Sent, quote.Status);
    }

    [Fact(DisplayName = "T-QUO-02: AddItem en estado Sent lanza InvalidOperationException")]
    public void AddItem_en_sent_lanza_excepcion()
    {
        var quote = BuildQuote();
        quote.AddItem(QuoteItemKind.Service, "Servicio", null, 1m, 100m, 21m, null);
        quote.Send(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            quote.AddItem(QuoteItemKind.Service, "Otro", null, 1m, 100m, 21m, null));
    }
}
