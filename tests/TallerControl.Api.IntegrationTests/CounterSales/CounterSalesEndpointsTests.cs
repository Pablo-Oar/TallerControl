using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TallerControl.Api.Identity;
using TallerControl.Api.IntegrationTests.Persistence;

namespace TallerControl.Api.IntegrationTests.CounterSales;

/// <summary>
/// E11: tests de integración de ventas de mostrador.
/// T-CSAL-01..03 (doc 08, REQ-E11, E11-C).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CounterSalesEndpointsTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private CounterSalesWebApplicationFactory _factory = null!;

    public CounterSalesEndpointsTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        _factory = await CounterSalesWebApplicationFactory.CreateAsync(_postgres);
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

    private async Task<HttpResponseMessage> PostCounterSaleAsync(
        HttpClient client, Guid? customerId = null)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/counter-sales")
        {
            Content = JsonContent.Create(new
            {
                customerId,
                items = new[]
                {
                    new
                    {
                        catalogItemId = _factory.CatalogItemId,
                        description = "Producto de prueba",
                        quantity = 1.0m,
                        unitPrice = 100m,
                        vatRate = 21m,
                    },
                },
            }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        return await client.SendAsync(req);
    }

    private static async Task<HttpResponseMessage> PostConfirmCounterSaleAsync(
        HttpClient client, Guid id, uint version)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/counter-sales/{id}/confirm")
        {
            Content = JsonContent.Create(new { accountId = (Guid?)null, version }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        return await client.SendAsync(req);
    }

    private static async Task<HttpResponseMessage> PostCancelCounterSaleAsync(
        HttpClient client, Guid id)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/counter-sales/{id}/cancel");
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        return await client.SendAsync(req);
    }

    // ── T-CSAL-01: POST /counter-sales crea venta en Draft → 201 ─────────────

    [Fact(DisplayName = "T-CSAL-01: POST /counter-sales crea venta en estado draft y devuelve 201")]
    public async Task Post_venta_mostrador_devuelve_201_en_draft()
    {
        using var client = CreateClient();
        await LoginAsync(client, CounterSalesWebApplicationFactory.OwnerEmail,
            CounterSalesWebApplicationFactory.OwnerPassword);

        var resp = await PostCounterSaleAsync(client, _factory.CustomerId);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal("draft", body.GetProperty("status").GetString(), StringComparer.OrdinalIgnoreCase);
        Assert.Equal(_factory.CustomerId, body.GetProperty("customerId").GetGuid());

        var location = resp.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.Contains(id.ToString(), location);
    }

    [Fact(DisplayName = "T-CSAL-01: POST /counter-sales sin cliente (anónimo) devuelve 201")]
    public async Task Post_venta_mostrador_sin_cliente_devuelve_201()
    {
        using var client = CreateClient();
        await LoginAsync(client, CounterSalesWebApplicationFactory.OwnerEmail,
            CounterSalesWebApplicationFactory.OwnerPassword);

        var resp = await PostCounterSaleAsync(client, customerId: null);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, body.GetProperty("customerId").ValueKind);
    }

    // ── T-CSAL-02: confirmar venta descuenta stock ────────────────────────────

    [Fact(DisplayName = "T-CSAL-02: POST /counter-sales/{id}/confirm confirma la venta → estado confirmed")]
    public async Task Confirmar_venta_cambia_estado_a_confirmed()
    {
        using var client = CreateClient();
        await LoginAsync(client, CounterSalesWebApplicationFactory.OwnerEmail,
            CounterSalesWebApplicationFactory.OwnerPassword);

        var createResp = await PostCounterSaleAsync(client);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        var version = created.GetProperty("version").GetUInt32();

        var confirmResp = await PostConfirmCounterSaleAsync(client, id, version);
        Assert.Equal(HttpStatusCode.OK, confirmResp.StatusCode);

        var confirmed = await confirmResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("confirmed", confirmed.GetProperty("status").GetString(), StringComparer.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "T-CSAL-02: confirmar venta descuenta el stock del ítem de catálogo")]
    public async Task Confirmar_venta_descuenta_stock()
    {
        using var client = CreateClient();
        await LoginAsync(client, CounterSalesWebApplicationFactory.OwnerEmail,
            CounterSalesWebApplicationFactory.OwnerPassword);

        // Stock inicial
        var stockBefore = await client.GetAsync($"/api/v1/catalog/{_factory.CatalogItemId}");
        stockBefore.EnsureSuccessStatusCode();
        var beforeDto = await stockBefore.Content.ReadFromJsonAsync<JsonElement>();
        var qtyBefore = beforeDto.GetProperty("stockQty").GetDecimal();

        var createResp = await PostCounterSaleAsync(client);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        var version = created.GetProperty("version").GetUInt32();

        await PostConfirmCounterSaleAsync(client, id, version);

        var stockAfter = await client.GetAsync($"/api/v1/catalog/{_factory.CatalogItemId}");
        stockAfter.EnsureSuccessStatusCode();
        var afterDto = await stockAfter.Content.ReadFromJsonAsync<JsonElement>();
        var qtyAfter = afterDto.GetProperty("stockQty").GetDecimal();

        // Vendimos 1 unidad → stock debe bajar 1
        Assert.Equal(qtyBefore - 1m, qtyAfter);
    }

    // ── T-CSAL-03: cancelar venta en borrador ────────────────────────────────

    [Fact(DisplayName = "T-CSAL-03: POST /counter-sales/{id}/cancel cancela una venta en draft → 200")]
    public async Task Cancelar_venta_en_draft_devuelve_200()
    {
        using var client = CreateClient();
        await LoginAsync(client, CounterSalesWebApplicationFactory.OwnerEmail,
            CounterSalesWebApplicationFactory.OwnerPassword);

        var createResp = await PostCounterSaleAsync(client);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var cancelResp = await PostCancelCounterSaleAsync(client, id);
        Assert.Equal(HttpStatusCode.OK, cancelResp.StatusCode);

        var cancelled = await cancelResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("cancelled", cancelled.GetProperty("status").GetString(), StringComparer.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "T-CSAL-03: cancelar venta Confirmed devuelve 422 (usar nota de crédito)")]
    public async Task Cancelar_venta_confirmada_devuelve_422()
    {
        using var client = CreateClient();
        await LoginAsync(client, CounterSalesWebApplicationFactory.OwnerEmail,
            CounterSalesWebApplicationFactory.OwnerPassword);

        var createResp = await PostCounterSaleAsync(client);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        var version = created.GetProperty("version").GetUInt32();

        await PostConfirmCounterSaleAsync(client, id, version);

        var cancelResp = await PostCancelCounterSaleAsync(client, id);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, cancelResp.StatusCode);
    }

    // ── T-CSAL-03: mecánico no puede crear ventas de mostrador ───────────────

    [Fact(DisplayName = "T-CSAL-03: mecánico recibe 403 al intentar crear una venta de mostrador")]
    public async Task Mecanico_no_puede_crear_venta_mostrador()
    {
        using var client = CreateClient();
        await LoginAsync(client, CounterSalesWebApplicationFactory.MechanicEmail,
            CounterSalesWebApplicationFactory.MechanicPassword);

        var resp = await PostCounterSaleAsync(client);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}
