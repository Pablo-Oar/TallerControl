using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TallerControl.Api.Identity;
using TallerControl.Api.IntegrationTests.Persistence;

namespace TallerControl.Api.IntegrationTests.Purchases;

/// <summary>
/// E11: tests de integración de compras.
/// T-PUR-01..05 (doc 08, REQ-E11, D-19).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PurchasesEndpointsTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private PurchasesWebApplicationFactory _factory = null!;

    public PurchasesEndpointsTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        _factory = await PurchasesWebApplicationFactory.CreateAsync(_postgres);
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

    private async Task<HttpResponseMessage> PostPurchaseAsync(
        HttpClient client,
        int? pointOfSale = 1,
        int? invoiceNumber = 1001,
        decimal perceptions = 0m,
        decimal retentions = 0m)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/purchases")
        {
            Content = JsonContent.Create(new
            {
                supplierId = _factory.SupplierId,
                invoiceDate = DateTimeOffset.UtcNow,
                pointOfSale,
                invoiceNumber,
                perceptions,
                retentions,
                items = new[]
                {
                    new
                    {
                        catalogItemId = _factory.CatalogItemId,
                        description = "Repuesto de prueba",
                        quantity = 2.0m,
                        unitCost = 300m,
                        vatRate = 21m,
                    },
                },
            }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        return await client.SendAsync(req);
    }

    private static async Task<HttpResponseMessage> PostConfirmPurchaseAsync(
        HttpClient client, Guid id, uint version)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/purchases/{id}/confirm")
        {
            Content = JsonContent.Create(new { accountId = (Guid?)null, version }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        return await client.SendAsync(req);
    }

    private static async Task<HttpResponseMessage> PostCancelPurchaseAsync(
        HttpClient client, Guid id, string? reason = null)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/purchases/{id}/cancel")
        {
            Content = JsonContent.Create(new { reason }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        return await client.SendAsync(req);
    }

    private static async Task<HttpResponseMessage> DeletePurchaseAsync(HttpClient client, Guid id)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/purchases/{id}")
        {
            Content = JsonContent.Create(new { reason = "Test: borrar compra" }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        return await client.SendAsync(req);
    }

    // ── T-PUR-01: POST /purchases crea compra en Draft → 201 ─────────────────

    [Fact(DisplayName = "T-PUR-01: POST /purchases crea compra en estado draft y devuelve 201")]
    public async Task Post_compra_devuelve_201_en_draft()
    {
        using var client = CreateClient();
        await LoginAsync(client, PurchasesWebApplicationFactory.OwnerEmail,
            PurchasesWebApplicationFactory.OwnerPassword);

        var resp = await PostPurchaseAsync(client);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal("draft", body.GetProperty("status").GetString(), StringComparer.OrdinalIgnoreCase);

        var location = resp.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.Contains(id.ToString(), location);
    }

    [Fact(DisplayName = "T-PUR-01: GET /purchases/{id} devuelve el detalle de la compra con ítems")]
    public async Task Get_compra_devuelve_detalle_con_items()
    {
        using var client = CreateClient();
        await LoginAsync(client, PurchasesWebApplicationFactory.OwnerEmail,
            PurchasesWebApplicationFactory.OwnerPassword);

        var createResp = await PostPurchaseAsync(client);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var getResp = await client.GetAsync($"/api/v1/purchases/{id}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);

        var dto = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(id, dto.GetProperty("id").GetGuid());
        var items = dto.GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(items);
        Assert.Equal("Repuesto de prueba", items[0].GetProperty("description").GetString());
    }

    // ── T-PUR-02: confirmar compra incrementa stock ───────────────────────────

    [Fact(DisplayName = "T-PUR-02: POST /purchases/{id}/confirm confirma la compra → estado confirmed")]
    public async Task Confirmar_compra_cambia_estado_a_confirmed()
    {
        using var client = CreateClient();
        await LoginAsync(client, PurchasesWebApplicationFactory.OwnerEmail,
            PurchasesWebApplicationFactory.OwnerPassword);

        var createResp = await PostPurchaseAsync(client);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        var version = created.GetProperty("version").GetUInt32();

        var confirmResp = await PostConfirmPurchaseAsync(client, id, version);
        Assert.Equal(HttpStatusCode.OK, confirmResp.StatusCode);

        var confirmed = await confirmResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("confirmed", confirmed.GetProperty("status").GetString(), StringComparer.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "T-PUR-02: confirmar compra incrementa el stock del ítem de catálogo")]
    public async Task Confirmar_compra_incrementa_stock()
    {
        using var client = CreateClient();
        await LoginAsync(client, PurchasesWebApplicationFactory.OwnerEmail,
            PurchasesWebApplicationFactory.OwnerPassword);

        // Estado inicial del stock
        var stockBefore = await client.GetAsync($"/api/v1/catalog/{_factory.CatalogItemId}");
        stockBefore.EnsureSuccessStatusCode();
        var beforeDto = await stockBefore.Content.ReadFromJsonAsync<JsonElement>();
        var qtyBefore = beforeDto.GetProperty("stockQty").GetDecimal();

        var createResp = await PostPurchaseAsync(client);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        var version = created.GetProperty("version").GetUInt32();

        await PostConfirmPurchaseAsync(client, id, version);

        var stockAfter = await client.GetAsync($"/api/v1/catalog/{_factory.CatalogItemId}");
        stockAfter.EnsureSuccessStatusCode();
        var afterDto = await stockAfter.Content.ReadFromJsonAsync<JsonElement>();
        var qtyAfter = afterDto.GetProperty("stockQty").GetDecimal();

        // Compramos 2 unidades → stock debe subir 2
        Assert.Equal(qtyBefore + 2m, qtyAfter);
    }

    // ── T-PUR-03: compra sin datos fiscales queda fuera del CITI ─────────────

    [Fact(DisplayName = "T-PUR-03: compra sin PointOfSale/InvoiceNumber tiene isExcludedFromCiti=true")]
    public async Task Compra_sin_datos_fiscales_excluida_del_citi()
    {
        using var client = CreateClient();
        await LoginAsync(client, PurchasesWebApplicationFactory.OwnerEmail,
            PurchasesWebApplicationFactory.OwnerPassword);

        // Sin punto de venta ni número de factura (D-19)
        var resp = await PostPurchaseAsync(client, pointOfSale: null, invoiceNumber: null);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("isExcludedFromCiti").GetBoolean());
    }

    [Fact(DisplayName = "T-PUR-03: compra con datos fiscales completos tiene isExcludedFromCiti=false")]
    public async Task Compra_con_datos_fiscales_incluida_en_citi()
    {
        using var client = CreateClient();
        await LoginAsync(client, PurchasesWebApplicationFactory.OwnerEmail,
            PurchasesWebApplicationFactory.OwnerPassword);

        var resp = await PostPurchaseAsync(client, pointOfSale: 1, invoiceNumber: 2000);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("isExcludedFromCiti").GetBoolean());
    }

    // ── T-PUR-04: cancelar compra en borrador ────────────────────────────────

    [Fact(DisplayName = "T-PUR-04: POST /purchases/{id}/cancel cancela una compra en draft → 200")]
    public async Task Cancelar_compra_en_draft_devuelve_200()
    {
        using var client = CreateClient();
        await LoginAsync(client, PurchasesWebApplicationFactory.OwnerEmail,
            PurchasesWebApplicationFactory.OwnerPassword);

        var createResp = await PostPurchaseAsync(client);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var cancelResp = await PostCancelPurchaseAsync(client, id);
        Assert.Equal(HttpStatusCode.OK, cancelResp.StatusCode);

        var cancelled = await cancelResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("cancelled", cancelled.GetProperty("status").GetString(), StringComparer.OrdinalIgnoreCase);
    }

    // ── T-PUR-05: DELETE solo funciona en Draft ───────────────────────────────

    [Fact(DisplayName = "T-PUR-05: DELETE /purchases/{id} en draft → 204")]
    public async Task Delete_compra_en_draft_devuelve_204()
    {
        using var client = CreateClient();
        await LoginAsync(client, PurchasesWebApplicationFactory.OwnerEmail,
            PurchasesWebApplicationFactory.OwnerPassword);

        var createResp = await PostPurchaseAsync(client);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var deleteResp = await DeletePurchaseAsync(client, id);
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        var getResp = await client.GetAsync($"/api/v1/purchases/{id}");
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    [Fact(DisplayName = "T-PUR-05: DELETE en compra Confirmed devuelve 422")]
    public async Task Delete_compra_confirmada_devuelve_422()
    {
        using var client = CreateClient();
        await LoginAsync(client, PurchasesWebApplicationFactory.OwnerEmail,
            PurchasesWebApplicationFactory.OwnerPassword);

        var createResp = await PostPurchaseAsync(client);
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        var version = created.GetProperty("version").GetUInt32();

        await PostConfirmPurchaseAsync(client, id, version);

        var deleteResp = await DeletePurchaseAsync(client, id);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, deleteResp.StatusCode);
    }

    // ── K-03: editar borrador, anular una confirmada con contraasientos ───────

    private static async Task<HttpResponseMessage> PutPurchaseAsync(HttpClient client, Guid id, object body)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/purchases/{id}") { Content = JsonContent.Create(body) };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        return await client.SendAsync(req);
    }

    [Fact(DisplayName = "T-PUR-06: Un borrador se edita (factura y renglones); uno confirmado no")]
    public async Task Editar_borrador_y_no_confirmada()
    {
        using var client = CreateClient();
        await LoginAsync(client, PurchasesWebApplicationFactory.OwnerEmail, PurchasesWebApplicationFactory.OwnerPassword);

        var created = await (await PostPurchaseAsync(client, pointOfSale: null, invoiceNumber: null)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(created.GetProperty("isExcludedFromCiti").GetBoolean());
        var id = created.GetProperty("id").GetGuid();

        object Body(uint version, decimal qty) => new
        {
            invoiceDate = DateTimeOffset.UtcNow,
            pointOfSale = 7,
            invoiceNumber = 4321,
            perceptions = 0m,
            retentions = 0m,
            items = new[] { new { catalogItemId = _factory.CatalogItemId, description = "Repuesto corregido", quantity = qty, unitCost = 100m, vatRate = 21m } },
            version,
        };

        var edited = await PutPurchaseAsync(client, id, Body(created.GetProperty("version").GetUInt32(), 5m));
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var dto = await edited.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(dto.GetProperty("isExcludedFromCiti").GetBoolean());
        Assert.Equal(500m, dto.GetProperty("subtotalNet").GetDecimal());
        Assert.Equal("Repuesto corregido", Assert.Single(dto.GetProperty("items").EnumerateArray()).GetProperty("description").GetString());

        // Una versión vieja se rechaza; una confirmada no se edita.
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await PutPurchaseAsync(client, id, Body(created.GetProperty("version").GetUInt32(), 1m))).StatusCode);
        var version = dto.GetProperty("version").GetUInt32();
        (await PostConfirmPurchaseAsync(client, id, version)).EnsureSuccessStatusCode();
        var confirmed = await (await client.GetAsync($"/api/v1/purchases/{id}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PutPurchaseAsync(client, id, Body(confirmed.GetProperty("version").GetUInt32(), 1m))).StatusCode);
    }

    [Fact(DisplayName = "T-PUR-07: Anular una compra confirmada pide motivo y deja contraasientos de stock y de caja")]
    public async Task Anular_compra_confirmada_con_contraasientos()
    {
        using var client = CreateClient();
        await LoginAsync(client, PurchasesWebApplicationFactory.OwnerEmail, PurchasesWebApplicationFactory.OwnerPassword);

        // Cuenta de caja para pagar la compra.
        var csrf = await FetchCsrfAsync(client);
        var accountReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/cash/accounts")
        {
            Content = JsonContent.Create(new { name = $"Caja compras {Guid.NewGuid():N}", kind = "cash" }),
        };
        accountReq.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        var account = await (await client.SendAsync(accountReq)).Content.ReadFromJsonAsync<JsonElement>();
        var accountId = account.GetProperty("id").GetGuid();

        var stockBefore = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/catalog/{_factory.CatalogItemId}")).GetProperty("stockQty").GetDecimal();

        var created = await (await PostPurchaseAsync(client)).Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        var confirmReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/purchases/{id}/confirm")
        {
            Content = JsonContent.Create(new { accountId, version = created.GetProperty("version").GetUInt32() }),
        };
        confirmReq.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, await FetchCsrfAsync(client));
        (await client.SendAsync(confirmReq)).EnsureSuccessStatusCode();

        // Sin motivo no se puede.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostCancelPurchaseAsync(client, id)).StatusCode);

        var cancelled = await PostCancelPurchaseAsync(client, id, "Se facturó mal");
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var dto = await cancelled.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("cancelled", dto.GetProperty("status").GetString());
        Assert.Equal("Se facturó mal", dto.GetProperty("cancelReason").GetString());

        // Stock: vuelve al valor de antes.
        var stockAfter = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/catalog/{_factory.CatalogItemId}")).GetProperty("stockQty").GetDecimal();
        Assert.Equal(stockBefore, stockAfter);

        // Caja: salida del pago y entrada de la anulación; el saldo queda en cero.
        var movements = await client.GetFromJsonAsync<JsonElement>($"/api/v1/cash/accounts/{accountId}/movements");
        var list = (movements.ValueKind == JsonValueKind.Array ? movements : movements.GetProperty("items")).EnumerateArray().ToList();
        Assert.Equal(2, list.Count);
        Assert.Contains(list, m => m.GetProperty("direction").GetString() == "in" && m.GetProperty("referenceType").GetString() == "purchase_cancel");
        var balance = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/cash/accounts/{accountId}")).GetProperty("balance").GetDecimal();
        Assert.Equal(0m, balance);
    }
}
