using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TallerControl.Api.Identity;
using TallerControl.Api.IntegrationTests.Persistence;

namespace TallerControl.Api.IntegrationTests.Suppliers;

/// <summary>
/// E11: tests de integración de proveedores.
/// T-SUP-01..03 (doc 08, REQ-E11).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SuppliersEndpointsTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private SuppliersWebApplicationFactory _factory = null!;

    public SuppliersEndpointsTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        _factory = await SuppliersWebApplicationFactory.CreateAsync(_postgres);
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

    private static async Task<HttpResponseMessage> PostSupplierAsync(
        HttpClient client, string name, string? cuit = null)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/suppliers")
        {
            Content = JsonContent.Create(new
            {
                name,
                cuit,
                vatCondition = "responsable_inscripto",
            }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        return await client.SendAsync(req);
    }

    private static async Task<HttpResponseMessage> PutSupplierAsync(
        HttpClient client, Guid id, uint version, string name)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/suppliers/{id}")
        {
            Content = JsonContent.Create(new
            {
                name,
                cuit = (string?)null,
                vatCondition = "responsable_inscripto",
            }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        req.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        return await client.SendAsync(req);
    }

    private static async Task<HttpResponseMessage> DeleteSupplierAsync(HttpClient client, Guid id)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/suppliers/{id}")
        {
            Content = JsonContent.Create(new { reason = "Test: baja proveedor" }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        return await client.SendAsync(req);
    }

    private static async Task<HttpResponseMessage> RestoreSupplierAsync(HttpClient client, Guid id)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/suppliers/{id}/restore");
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        return await client.SendAsync(req);
    }

    // ── T-SUP-01: POST /suppliers crea proveedor → 201 ───────────────────────

    [Fact(DisplayName = "T-SUP-01: POST /suppliers crea proveedor y devuelve 201 con Location")]
    public async Task Post_proveedor_devuelve_201_con_location()
    {
        using var client = CreateClient();
        await LoginAsync(client, SuppliersWebApplicationFactory.OwnerEmail,
            SuppliersWebApplicationFactory.OwnerPassword);

        var resp = await PostSupplierAsync(client, "Proveedor T-SUP-01");

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal("Proveedor T-SUP-01", body.GetProperty("name").GetString());

        var location = resp.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.Contains(id.ToString(), location);
    }

    // ── T-SUP-02: GET y PUT de proveedores ───────────────────────────────────

    [Fact(DisplayName = "T-SUP-02: GET /suppliers/{id} devuelve el detalle del proveedor")]
    public async Task Get_proveedor_devuelve_detalle()
    {
        using var client = CreateClient();
        await LoginAsync(client, SuppliersWebApplicationFactory.OwnerEmail,
            SuppliersWebApplicationFactory.OwnerPassword);

        var createResp = await PostSupplierAsync(client, "Proveedor GET T-SUP-02");
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var getResp = await client.GetAsync($"/api/v1/suppliers/{id}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);

        var dto = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(id, dto.GetProperty("id").GetGuid());
        Assert.Equal("Proveedor GET T-SUP-02", dto.GetProperty("name").GetString());
    }

    [Fact(DisplayName = "T-SUP-02: PUT /suppliers/{id} actualiza el nombre del proveedor")]
    public async Task Put_proveedor_actualiza_nombre()
    {
        using var client = CreateClient();
        await LoginAsync(client, SuppliersWebApplicationFactory.OwnerEmail,
            SuppliersWebApplicationFactory.OwnerPassword);

        var createResp = await PostSupplierAsync(client, "Original T-SUP-02b");
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        var version = created.GetProperty("version").GetUInt32();

        var putResp = await PutSupplierAsync(client, id, version, "Actualizado T-SUP-02b");

        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);
        var updated = await putResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Actualizado T-SUP-02b", updated.GetProperty("name").GetString());
    }

    [Fact(DisplayName = "T-SUP-02: mecánico no puede crear proveedores")]
    public async Task Mecanico_no_puede_crear_proveedor()
    {
        using var client = CreateClient();
        await LoginAsync(client, SuppliersWebApplicationFactory.MechanicEmail,
            SuppliersWebApplicationFactory.MechanicPassword);

        var resp = await PostSupplierAsync(client, "No autorizado T-SUP-02c");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ── T-SUP-03: borrado lógico y restauración ───────────────────────────────

    [Fact(DisplayName = "T-SUP-03: DELETE /suppliers/{id} hace borrado lógico → 204; GET devuelve 404")]
    public async Task Delete_logico_devuelve_204_y_get_devuelve_404()
    {
        using var client = CreateClient();
        await LoginAsync(client, SuppliersWebApplicationFactory.OwnerEmail,
            SuppliersWebApplicationFactory.OwnerPassword);

        var createResp = await PostSupplierAsync(client, "Borrado T-SUP-03");
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var deleteResp = await DeleteSupplierAsync(client, id);
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        var getResp = await client.GetAsync($"/api/v1/suppliers/{id}");
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    [Fact(DisplayName = "T-SUP-03: POST /{id}/restore restaura el proveedor → 200")]
    public async Task Restore_devuelve_200_con_proveedor_activo()
    {
        using var client = CreateClient();
        await LoginAsync(client, SuppliersWebApplicationFactory.OwnerEmail,
            SuppliersWebApplicationFactory.OwnerPassword);

        var createResp = await PostSupplierAsync(client, "Restaurar T-SUP-03b");
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        await DeleteSupplierAsync(client, id);

        var restoreResp = await RestoreSupplierAsync(client, id);
        Assert.Equal(HttpStatusCode.OK, restoreResp.StatusCode);

        var restored = await restoreResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(id, restored.GetProperty("id").GetGuid());
        Assert.Equal("Restaurar T-SUP-03b", restored.GetProperty("name").GetString());
    }

    // ── K-02: datos de contacto, ver dados de baja ────────────────────────────

    [Fact(DisplayName = "T-SUP-04: Los datos de contacto se guardan y «dados de baja» lista solo las bajas, que se restauran")]
    public async Task Contacto_bajas_y_restauracion()
    {
        using var client = CreateClient();
        await LoginAsync(client, SuppliersWebApplicationFactory.OwnerEmail, SuppliersWebApplicationFactory.OwnerPassword);

        var name = $"Repuestos del Sur {Guid.NewGuid():N}";
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/suppliers")
        {
            Content = JsonContent.Create(new
            {
                name,
                vatCondition = "responsable_inscripto",
                address = "Av. Siempre Viva 742",
                phone = "011 4444-5555",
                email = "VENTAS@SUR.COM",
                notes = "Entrega los martes",
            }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        var created = await (await client.SendAsync(req)).Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal("Av. Siempre Viva 742", created.GetProperty("address").GetString());
        Assert.Equal("ventas@sur.com", created.GetProperty("email").GetString());

        // Sin compras: el detalle informa cero (el aviso de baja solo aparece con compras vigentes).
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/v1/suppliers/{id}");
        Assert.Equal(0, detail.GetProperty("purchaseCount").GetInt32());

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteSupplierAsync(client, id)).StatusCode);

        var vigentes = await client.GetFromJsonAsync<JsonElement>($"/api/v1/suppliers?q={Uri.EscapeDataString(name)}");
        Assert.Empty(vigentes.GetProperty("items").EnumerateArray());
        var bajas = await client.GetFromJsonAsync<JsonElement>($"/api/v1/suppliers?deleted=true&q={Uri.EscapeDataString(name)}");
        Assert.Single(bajas.GetProperty("items").EnumerateArray());

        var restore = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/suppliers/{id}/restore");
        restore.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, await FetchCsrfAsync(client));
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(restore)).StatusCode);
    }
}
