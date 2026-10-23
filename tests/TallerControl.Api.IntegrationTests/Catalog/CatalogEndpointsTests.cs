using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TallerControl.Api.Identity;
using TallerControl.Api.IntegrationTests.Persistence;

namespace TallerControl.Api.IntegrationTests.Catalog;

/// <summary>
/// E4 bloque 2: tests de integración de catálogo y stock.
/// T-CAT-01..06, T-STK-01..02 (doc 08 §4, doc 05 §2.5, REQ-08).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CatalogEndpointsTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private CatalogWebApplicationFactory _factory = null!;

    public CatalogEndpointsTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        _factory = await CatalogWebApplicationFactory.CreateAsync(_postgres);
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

    private static async Task<string> FetchCsrfAsync(HttpClient client)
    {
        var resp = await client.GetAsync("/api/v1/auth/antiforgery-token");
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("token").GetString()!;
    }

    private static async Task LoginAsync(HttpClient client, string email, string password)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email, password, rememberMe = false }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostCatalogItemAsync(
        HttpClient client,
        string name,
        string kind = "Service",
        bool trackStock = false,
        decimal netPrice = 100m)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/catalog")
        {
            Content = JsonContent.Create(new { kind, name, netPrice, vatRate = 21, trackStock }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        return await client.SendAsync(req);
    }

    private static async Task<HttpResponseMessage> PutCatalogItemAsync(
        HttpClient client, Guid id, uint version, object body)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/catalog/{id}")
        {
            Content = JsonContent.Create(body),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        req.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        return await client.SendAsync(req);
    }

    private static async Task<HttpResponseMessage> DeleteCatalogItemAsync(HttpClient client, Guid id)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/catalog/{id}")
        {
            Content = JsonContent.Create(new { reason = "Test: baja" }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        return await client.SendAsync(req);
    }

    private static async Task<HttpResponseMessage> RestoreCatalogItemAsync(HttpClient client, Guid id)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/catalog/{id}/restore");
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        return await client.SendAsync(req);
    }

    private static async Task<HttpResponseMessage> PostStockAdjustmentAsync(
        HttpClient client, Guid id, decimal quantity, string note = "Ajuste de prueba")
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/catalog/{id}/stock-adjustments")
        {
            Content = JsonContent.Create(new { quantity, note }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        return await client.SendAsync(req);
    }

    // ── T-CAT-01: POST /api/v1/catalog crea ítem → 201 ───────────────────────

    [Fact(DisplayName = "T-CAT-01: POST /catalog crea ítem y devuelve 201 con Location")]
    public async Task Post_item_devuelve_201_con_location()
    {
        using var client = CreateClient();
        await LoginAsync(client, CatalogWebApplicationFactory.OwnerEmail,
            CatalogWebApplicationFactory.OwnerPassword);

        var resp = await PostCatalogItemAsync(client, "Cambio de aceite T-CAT-01");

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal("Cambio de aceite T-CAT-01", body.GetProperty("name").GetString());
        Assert.Equal("Service", body.GetProperty("kind").GetString(),
            StringComparer.OrdinalIgnoreCase);

        var location = resp.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.Contains(id.ToString(), location);
    }

    // ── T-CAT-02: GET /api/v1/catalog/{id} devuelve detalle ──────────────────

    [Fact(DisplayName = "T-REP-03: GET /reports/stock lista los ítems con control de stock (no devuelve 500)")]
    public async Task Reporte_de_stock_lista_items_con_control_de_stock()
    {
        using var client = CreateClient();
        await LoginAsync(client, CatalogWebApplicationFactory.OwnerEmail,
            CatalogWebApplicationFactory.OwnerPassword);

        var created = await PostCatalogItemAsync(client, "Item stock reporte", kind: "Product", trackStock: true);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var resp = await client.GetAsync("/api/v1/reports/stock");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var rows = json.GetProperty("rows").EnumerateArray().ToList();
        Assert.Contains(rows, r => r.GetProperty("name").GetString() == "Item stock reporte");
        Assert.True(json.TryGetProperty("lowStockCount", out _));
    }

    [Fact(DisplayName = "T-CAT-02: GET /catalog/{id} devuelve el detalle del ítem")]
    public async Task Get_item_devuelve_detalle()
    {
        using var client = CreateClient();
        await LoginAsync(client, CatalogWebApplicationFactory.OwnerEmail,
            CatalogWebApplicationFactory.OwnerPassword);

        var createResp = await PostCatalogItemAsync(client, "Filtro aire T-CAT-02", kind: "Product");
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var getResp = await client.GetAsync($"/api/v1/catalog/{id}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);

        var dto = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(id, dto.GetProperty("id").GetGuid());
        Assert.Equal("Filtro aire T-CAT-02", dto.GetProperty("name").GetString());
        Assert.Equal(0m, dto.GetProperty("stockQty").GetDecimal());
    }

    [Fact(DisplayName = "T-CAT-08: Crear un producto con control de stock y stock inicial lo deja como movimiento")]
    public async Task Crear_con_stock_inicial_registra_movimiento()
    {
        using var client = CreateClient();
        await LoginAsync(client, CatalogWebApplicationFactory.OwnerEmail, CatalogWebApplicationFactory.OwnerPassword);

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/catalog")
        {
            Content = JsonContent.Create(new { kind = "product", name = "Aceite 5W30 T-CAT-08", netPrice = 100m, vatRate = 21, trackStock = true, initialStockQty = 12m }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, await FetchCsrfAsync(client));
        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var created = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(12m, created.GetProperty("stockQty").GetDecimal());

        var movements = await client.GetFromJsonAsync<JsonElement>($"/api/v1/catalog/{created.GetProperty("id").GetGuid()}/stock-movements");
        Assert.Contains(movements.EnumerateArray(), m => m.GetProperty("quantity").GetDecimal() == 12m);
    }

    // ── T-CAT-03: PUT no toca stock (solo metadatos) ──────────────────────────

    [Fact(DisplayName = "T-CAT-03: PUT /catalog/{id} actualiza nombre sin tocar el stock")]
    public async Task Put_item_actualiza_nombre_sin_modificar_stock()
    {
        using var client = CreateClient();
        await LoginAsync(client, CatalogWebApplicationFactory.OwnerEmail,
            CatalogWebApplicationFactory.OwnerPassword);

        var createResp = await PostCatalogItemAsync(client, "Original T-CAT-03", kind: "Product", trackStock: true);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        var version = created.GetProperty("version").GetUInt32();

        var putResp = await PutCatalogItemAsync(client, id, version, new
        {
            name = "Actualizado T-CAT-03",
            netPrice = 200m,
            vatRate = 21,
            isActive = true,
        });

        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);
        var updated = await putResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Actualizado T-CAT-03", updated.GetProperty("name").GetString());
        Assert.Equal(0m, updated.GetProperty("stockQty").GetDecimal());
    }

    [Fact(DisplayName = "T-CAT-03: PUT sin If-Match devuelve 422")]
    public async Task Put_sin_if_match_devuelve_422()
    {
        using var client = CreateClient();
        await LoginAsync(client, CatalogWebApplicationFactory.OwnerEmail,
            CatalogWebApplicationFactory.OwnerPassword);

        var createResp = await PostCatalogItemAsync(client, "Sin etag T-CAT-03b");
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/catalog/{id}")
        {
            Content = JsonContent.Create(new { name = "Sin etag" }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);

        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    // ── T-CAT-04: cambio de precio no afecta ítems de órdenes ya cargadas ─────
    // (stub: validamos que el precio se actualiza correctamente en el catálogo)

    [Fact(DisplayName = "T-CAT-04: cambio de precio queda reflejado en el detalle del ítem")]
    public async Task Cambio_de_precio_se_refleja_en_detalle()
    {
        using var client = CreateClient();
        await LoginAsync(client, CatalogWebApplicationFactory.OwnerEmail,
            CatalogWebApplicationFactory.OwnerPassword);

        var createResp = await PostCatalogItemAsync(client, "Precio T-CAT-04", netPrice: 500m);
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        var version = created.GetProperty("version").GetUInt32();

        var putResp = await PutCatalogItemAsync(client, id, version, new
        {
            name = "Precio T-CAT-04",
            netPrice = 750m,
            vatRate = 21,
            isActive = true,
        });

        putResp.EnsureSuccessStatusCode();
        var updated = await putResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(750m, updated.GetProperty("netPrice").GetDecimal());
    }

    // ── T-CAT-05: borrado lógico y restauración ───────────────────────────────

    [Fact(DisplayName = "T-CAT-05: DELETE /catalog/{id} hace borrado lógico → 204; GET devuelve 404")]
    public async Task Delete_logico_devuelve_204_y_get_devuelve_404()
    {
        using var client = CreateClient();
        await LoginAsync(client, CatalogWebApplicationFactory.OwnerEmail,
            CatalogWebApplicationFactory.OwnerPassword);

        var createResp = await PostCatalogItemAsync(client, "Borrado T-CAT-05");
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var deleteResp = await DeleteCatalogItemAsync(client, id);
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        var getResp = await client.GetAsync($"/api/v1/catalog/{id}");
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    [Fact(DisplayName = "T-CAT-05: POST /{id}/restore restaura el ítem → 200")]
    public async Task Restore_devuelve_200_con_item_activo()
    {
        using var client = CreateClient();
        await LoginAsync(client, CatalogWebApplicationFactory.OwnerEmail,
            CatalogWebApplicationFactory.OwnerPassword);

        var createResp = await PostCatalogItemAsync(client, "Restaurar T-CAT-05b");
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        await DeleteCatalogItemAsync(client, id);

        var restoreResp = await RestoreCatalogItemAsync(client, id);
        Assert.Equal(HttpStatusCode.OK, restoreResp.StatusCode);

        var restored = await restoreResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(id, restored.GetProperty("id").GetGuid());
        Assert.True(restored.GetProperty("isActive").GetBoolean());
    }

    // ── T-STK-01: ajuste manual de stock ─────────────────────────────────────

    [Fact(DisplayName = "T-STK-01: POST /{id}/stock-adjustments ajusta el stock y devuelve el ítem actualizado")]
    public async Task Stock_adjustment_actualiza_qty_y_devuelve_item()
    {
        using var client = CreateClient();
        await LoginAsync(client, CatalogWebApplicationFactory.OwnerEmail,
            CatalogWebApplicationFactory.OwnerPassword);

        var createResp = await PostCatalogItemAsync(client, "Filtro T-STK-01", kind: "Product", trackStock: true);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var adjustResp = await PostStockAdjustmentAsync(client, id, 10, "Ingreso inicial");
        Assert.Equal(HttpStatusCode.OK, adjustResp.StatusCode);

        var updated = await adjustResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(10m, updated.GetProperty("stockQty").GetDecimal());
    }

    [Fact(DisplayName = "T-STK-01: ajuste de stock con nota vacía devuelve 422")]
    public async Task Ajuste_sin_nota_devuelve_422()
    {
        using var client = CreateClient();
        await LoginAsync(client, CatalogWebApplicationFactory.OwnerEmail,
            CatalogWebApplicationFactory.OwnerPassword);

        var createResp = await PostCatalogItemAsync(client, "Filtro T-STK-01b", kind: "Product", trackStock: true);
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/catalog/{id}/stock-adjustments")
        {
            Content = JsonContent.Create(new { quantity = 5, note = "" }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);

        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    // ── T-STK-02: libro de movimientos ───────────────────────────────────────

    [Fact(DisplayName = "T-STK-02: GET /{id}/stock-movements devuelve historial de movimientos")]
    public async Task Get_stock_movements_devuelve_historial()
    {
        using var client = CreateClient();
        await LoginAsync(client, CatalogWebApplicationFactory.OwnerEmail,
            CatalogWebApplicationFactory.OwnerPassword);

        var createResp = await PostCatalogItemAsync(client, "Movimientos T-STK-02", kind: "Product", trackStock: true);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        // Dos ajustes
        await PostStockAdjustmentAsync(client, id, 10, "Primera entrada");
        await PostStockAdjustmentAsync(client, id, -3, "Primera salida");

        var movResp = await client.GetAsync($"/api/v1/catalog/{id}/stock-movements");
        Assert.Equal(HttpStatusCode.OK, movResp.StatusCode);

        var movements = await movResp.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(movements);
        Assert.Equal(2, movements.Length);
    }

    // ── T-CAT-06: mecánico no puede crear ni borrar ítems ────────────────────

    [Fact(DisplayName = "T-CAT-06: mecánico recibe 403 al intentar crear un ítem")]
    public async Task Mecanico_no_puede_crear_item()
    {
        using var client = CreateClient();
        await LoginAsync(client, CatalogWebApplicationFactory.MechanicEmail,
            CatalogWebApplicationFactory.MechanicPassword);

        var resp = await PostCatalogItemAsync(client, "No autorizado T-CAT-06");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact(DisplayName = "T-CAT-06: mecánico puede listar ítems del catálogo")]
    public async Task Mecanico_puede_listar_catalog()
    {
        using var client = CreateClient();
        await LoginAsync(client, CatalogWebApplicationFactory.MechanicEmail,
            CatalogWebApplicationFactory.MechanicPassword);

        var resp = await client.GetAsync("/api/v1/catalog");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ── Orden en el servidor (sort/dir) ──────────────────────────────────────

    [Theory(DisplayName = "T-CAT-07: GET /catalog ordena por precio en el servidor (sort/dir)")]
    [InlineData("asc")]
    [InlineData("desc")]
    public async Task Listado_ordena_por_precio_en_el_servidor(string dir)
    {
        using var client = CreateClient();
        await LoginAsync(client, CatalogWebApplicationFactory.OwnerEmail,
            CatalogWebApplicationFactory.OwnerPassword);

        var cheap = $"Barato {Guid.NewGuid():N}";
        var dear = $"Caro {Guid.NewGuid():N}";
        (await PostCatalogItemAsync(client, cheap, netPrice: 1m)).EnsureSuccessStatusCode();
        (await PostCatalogItemAsync(client, dear, netPrice: 9_999_999m)).EnsureSuccessStatusCode();

        var resp = await client.GetAsync($"/api/v1/catalog?sort=price&dir={dir}&pageSize=100");
        resp.EnsureSuccessStatusCode();
        var names = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items")
            .EnumerateArray().Select(i => i.GetProperty("name").GetString()).ToList();

        var (first, second) = dir == "asc" ? (cheap, dear) : (dear, cheap);
        Assert.True(names.IndexOf(first) < names.IndexOf(second));
    }

    [Fact(DisplayName = "T-CAT-07b: una clave de orden desconocida no rompe el listado del catálogo")]
    public async Task Listado_del_catalogo_ignora_claves_de_orden_desconocidas()
    {
        using var client = CreateClient();
        await LoginAsync(client, CatalogWebApplicationFactory.OwnerEmail,
            CatalogWebApplicationFactory.OwnerPassword);

        var resp = await client.GetAsync("/api/v1/catalog?sort=1;drop table catalog_items&dir=desc");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ── K-01: descripción, stock máximo, costo con permiso, eliminados, control de stock ───

    private static async Task<JsonElement> CreateJsonAsync(HttpClient client, object body)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/catalog") { Content = JsonContent.Create(body) };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact(DisplayName = "T-CAT-08: Descripción, stock máximo y costo se guardan; el mecánico no ve el costo")]
    public async Task Descripcion_stock_maximo_y_costo_se_guardan()
    {
        using var owner = CreateClient();
        using var mechanic = CreateClient();
        await LoginAsync(owner, CatalogWebApplicationFactory.OwnerEmail, CatalogWebApplicationFactory.OwnerPassword);
        await LoginAsync(mechanic, CatalogWebApplicationFactory.MechanicEmail, CatalogWebApplicationFactory.MechanicPassword);

        var name = $"Amortiguador {Guid.NewGuid():N}";
        var created = await CreateJsonAsync(owner, new
        {
            kind = "Product",
            name,
            netPrice = 500m,
            vatRate = 21,
            cost = 320m,
            trackStock = true,
            stockMin = 2m,
            stockMax = 10m,
            description = "Delantero, par. Garantía 6 meses.",
        });
        Assert.Equal("Delantero, par. Garantía 6 meses.", created.GetProperty("description").GetString());
        Assert.Equal(10m, created.GetProperty("stockMax").GetDecimal());
        Assert.Equal(320m, created.GetProperty("cost").GetDecimal());

        // El stock máximo no puede ser menor que el mínimo.
        var csrf = await FetchCsrfAsync(owner);
        var bad = new HttpRequestMessage(HttpMethod.Post, "/api/v1/catalog")
        {
            Content = JsonContent.Create(new { kind = "Product", name = name + "x", netPrice = 1m, vatRate = 21, trackStock = true, stockMin = 5m, stockMax = 3m }),
        };
        bad.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await owner.SendAsync(bad)).StatusCode);

        // El mecánico lee el ítem pero sin costo (ni en el detalle ni en el listado).
        var id = created.GetProperty("id").GetGuid();
        var asMechanic = await mechanic.GetFromJsonAsync<JsonElement>($"/api/v1/catalog/{id}");
        Assert.Equal(JsonValueKind.Null, asMechanic.GetProperty("cost").ValueKind);
        var list = await mechanic.GetFromJsonAsync<JsonElement>($"/api/v1/catalog?q={Uri.EscapeDataString(name)}");
        Assert.All(list.GetProperty("items").EnumerateArray(), x => Assert.Equal(JsonValueKind.Null, x.GetProperty("cost").ValueKind));
    }

    [Fact(DisplayName = "T-CAT-09: «Ver eliminados» lista solo los dados de baja, y se restauran")]
    public async Task Ver_eliminados_y_restaurar()
    {
        using var client = CreateClient();
        await LoginAsync(client, CatalogWebApplicationFactory.OwnerEmail, CatalogWebApplicationFactory.OwnerPassword);

        var name = $"Descartable {Guid.NewGuid():N}";
        var id = (await CreateJsonAsync(client, new { kind = "Service", name, netPrice = 10m, vatRate = 21 })).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await DeleteCatalogItemAsync(client, id)).StatusCode);

        var vigentes = await client.GetFromJsonAsync<JsonElement>($"/api/v1/catalog?q={Uri.EscapeDataString(name)}");
        Assert.Empty(vigentes.GetProperty("items").EnumerateArray());

        var eliminados = await client.GetFromJsonAsync<JsonElement>($"/api/v1/catalog?deleted=true&q={Uri.EscapeDataString(name)}");
        Assert.Single(eliminados.GetProperty("items").EnumerateArray());

        Assert.Equal(HttpStatusCode.OK, (await RestoreCatalogItemAsync(client, id)).StatusCode);
        var otraVez = await client.GetFromJsonAsync<JsonElement>($"/api/v1/catalog?q={Uri.EscapeDataString(name)}");
        Assert.Single(otraVez.GetProperty("items").EnumerateArray());
    }

    [Fact(DisplayName = "T-CAT-10: Activar el control de stock con cantidad inicial deja un movimiento; desactivarlo con existencias se rechaza")]
    public async Task Activar_control_de_stock_con_cantidad_inicial()
    {
        using var client = CreateClient();
        await LoginAsync(client, CatalogWebApplicationFactory.OwnerEmail, CatalogWebApplicationFactory.OwnerPassword);

        var created = await CreateJsonAsync(client, new { kind = "Product", name = $"Bujía {Guid.NewGuid():N}", netPrice = 50m, vatRate = 21, trackStock = false });
        var id = created.GetProperty("id").GetGuid();

        var on = await PutCatalogItemAsync(client, id, created.GetProperty("version").GetUInt32(), new
        {
            name = created.GetProperty("name").GetString(),
            netPrice = 50m,
            vatRate = 21,
            isActive = true,
            trackStock = true,
            initialStockQty = 12m,
        });
        Assert.Equal(HttpStatusCode.OK, on.StatusCode);
        var tracked = await on.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(tracked.GetProperty("trackStock").GetBoolean());
        Assert.Equal(12m, tracked.GetProperty("stockQty").GetDecimal());

        var movements = await client.GetFromJsonAsync<JsonElement>($"/api/v1/catalog/{id}/stock-movements");
        var first = Assert.Single(movements.EnumerateArray());
        Assert.Equal(12m, first.GetProperty("quantity").GetDecimal());
        Assert.Contains("Stock inicial", first.GetProperty("note").GetString());

        // Con existencias no se puede dejar de controlar el stock.
        var off = await PutCatalogItemAsync(client, id, tracked.GetProperty("version").GetUInt32(), new
        {
            name = tracked.GetProperty("name").GetString(),
            netPrice = 50m,
            vatRate = 21,
            isActive = true,
            trackStock = false,
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, off.StatusCode);
    }
}
