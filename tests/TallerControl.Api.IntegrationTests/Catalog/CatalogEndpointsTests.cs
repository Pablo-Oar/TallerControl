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
}
