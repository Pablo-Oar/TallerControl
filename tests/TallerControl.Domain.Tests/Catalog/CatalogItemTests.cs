using TallerControl.Domain.Catalog;

namespace TallerControl.Domain.Tests.Catalog;

/// <summary>
/// T-CAT-01: creación de ítems con invariantes (REQ-08, CatalogItem).
/// T-CAT-03: precio se puede cambiar; stock no se toca en Update.
/// T-CAT-04: ApplyStockMovement suma/resta; cantidad cero y servicio con stock lanzan excepción.
/// </summary>
public class CatalogItemTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();

    // ── T-CAT-01: invariantes de creación ────────────────────────────────────

    [Fact(DisplayName = "T-CAT-01: nombre vacío lanza ArgumentException")]
    public void Nombre_vacio_lanza_excepcion()
    {
        Assert.Throws<ArgumentException>(() =>
            new CatalogItem(TenantId, CatalogItemKind.Service, "", null, 100, 21, null, false, null, null, null));
    }

    [Fact(DisplayName = "T-CAT-01: precio neto negativo lanza ArgumentOutOfRangeException")]
    public void Precio_negativo_lanza_excepcion()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CatalogItem(TenantId, CatalogItemKind.Service, "Servicio", null, -1, 21, null, false, null, null, null));
    }

    [Fact(DisplayName = "T-CAT-01: servicio con trackStock=true lanza ArgumentException")]
    public void Servicio_con_stock_lanza_excepcion()
    {
        Assert.Throws<ArgumentException>(() =>
            new CatalogItem(TenantId, CatalogItemKind.Service, "Servicio", null, 100, 21, null, true, null, null, null));
    }

    [Fact(DisplayName = "T-CAT-01: producto con trackStock=false no lanza excepción")]
    public void Producto_sin_stock_es_valido()
    {
        var item = new CatalogItem(TenantId, CatalogItemKind.Product, "Producto", null, 100, 21, null, false, null, null, null);
        Assert.False(item.TrackStock);
        Assert.Equal(0m, item.StockQty);
        Assert.True(item.IsActive);
    }

    [Fact(DisplayName = "T-CAT-01: SKU se normaliza a mayúsculas")]
    public void Sku_se_normaliza_a_mayusculas()
    {
        var item = new CatalogItem(TenantId, CatalogItemKind.Product, "Aceite", "abc-123", 100, 21, null, false, null, null, null);
        Assert.Equal("ABC-123", item.Sku);
    }

    // ── T-CAT-03: Update solo toca metadatos, no el stock ────────────────────

    [Fact(DisplayName = "T-CAT-03: Update cambia precio sin tocar el stock")]
    public void Update_cambia_precio_sin_modificar_stock()
    {
        var item = new CatalogItem(TenantId, CatalogItemKind.Product, "Filtro", null, 100, 21, null, true, 5, null, null);
        item.ApplyStockMovement(20);

        item.Update("Filtro Pro", null, 200, 21, null, 5, null, null, true);

        Assert.Equal(200m, item.NetPrice);
        Assert.Equal(20m, item.StockQty); // stock intacto
    }

    // ── T-CAT-04: ApplyStockMovement ─────────────────────────────────────────

    [Fact(DisplayName = "T-CAT-04: ApplyStockMovement suma la cantidad al stock")]
    public void ApplyStockMovement_suma_al_stock()
    {
        var item = new CatalogItem(TenantId, CatalogItemKind.Product, "Bujía", null, 50, 21, null, true, null, null, null);
        item.ApplyStockMovement(10);
        Assert.Equal(10m, item.StockQty);
    }

    [Fact(DisplayName = "T-CAT-04: D-07 permite stock negativo")]
    public void ApplyStockMovement_permite_stock_negativo()
    {
        var item = new CatalogItem(TenantId, CatalogItemKind.Product, "Bujía", null, 50, 21, null, true, null, null, null);
        item.ApplyStockMovement(-5); // D-07: stock negativo permitido con alerta
        Assert.Equal(-5m, item.StockQty);
    }

    [Fact(DisplayName = "T-CAT-04: cantidad cero en ApplyStockMovement lanza ArgumentException")]
    public void ApplyStockMovement_con_cero_lanza_excepcion()
    {
        var item = new CatalogItem(TenantId, CatalogItemKind.Product, "Bujía", null, 50, 21, null, true, null, null, null);
        Assert.Throws<ArgumentException>(() => item.ApplyStockMovement(0));
    }

    [Fact(DisplayName = "T-CAT-04: ApplyStockMovement en servicio sin trackStock lanza InvalidOperationException")]
    public void ApplyStockMovement_en_servicio_sin_stock_lanza_excepcion()
    {
        var item = new CatalogItem(TenantId, CatalogItemKind.Service, "Mano de obra", null, 100, 21, null, false, null, null, null);
        Assert.Throws<InvalidOperationException>(() => item.ApplyStockMovement(5));
    }

    // ── IsLowStock ───────────────────────────────────────────────────────────

    [Fact(DisplayName = "IsLowStock: verdadero cuando stock < mínimo")]
    public void IsLowStock_verdadero_cuando_stock_debajo_del_minimo()
    {
        var item = new CatalogItem(TenantId, CatalogItemKind.Product, "Aceite", null, 100, 21, null, true, 5, null, null);
        item.ApplyStockMovement(3);
        Assert.True(item.IsLowStock); // 3 < 5
    }

    [Fact(DisplayName = "IsLowStock: falso cuando stock >= mínimo")]
    public void IsLowStock_falso_cuando_stock_ok()
    {
        var item = new CatalogItem(TenantId, CatalogItemKind.Product, "Aceite", null, 100, 21, null, true, 5, null, null);
        item.ApplyStockMovement(10);
        Assert.False(item.IsLowStock); // 10 >= 5
    }

    [Fact(DisplayName = "IsLowStock: falso si no hay mínimo configurado")]
    public void IsLowStock_falso_si_sin_minimo()
    {
        var item = new CatalogItem(TenantId, CatalogItemKind.Product, "Aceite", null, 100, 21, null, true, null, null, null);
        Assert.False(item.IsLowStock);
    }
}
