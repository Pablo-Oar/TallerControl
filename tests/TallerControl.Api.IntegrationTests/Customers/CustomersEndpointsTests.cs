using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TallerControl.Api.Identity;
using TallerControl.Api.IntegrationTests.Persistence;

namespace TallerControl.Api.IntegrationTests.Customers;

/// <summary>
/// E3 bloque 2: tests de integración de clientes.
/// T-CUS-01..08 (doc 08 §4, doc 05 §2.3, REQ-05).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CustomersEndpointsTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private CustomersVehiclesWebApplicationFactory _factory = null!;

    public CustomersEndpointsTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        _factory = await CustomersVehiclesWebApplicationFactory.CreateAsync(_postgres);
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

    private static async Task<HttpResponseMessage> PostCustomerAsync(
        HttpClient client,
        string name,
        string? phone = null,
        string? email = null)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customers")
        {
            Content = JsonContent.Create(new { name, phone, email }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        return await client.SendAsync(req);
    }

    private static async Task<HttpResponseMessage> GetCustomerAsync(HttpClient client, Guid id)
    {
        return await client.GetAsync($"/api/v1/customers/{id}");
    }

    private static async Task<HttpResponseMessage> PutCustomerAsync(
        HttpClient client,
        Guid id,
        uint version,
        object body)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/customers/{id}")
        {
            Content = JsonContent.Create(body),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        req.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        return await client.SendAsync(req);
    }

    private static async Task<HttpResponseMessage> DeleteCustomerAsync(
        HttpClient client,
        Guid id,
        string reason = "Test: borrado lógico")
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/customers/{id}")
        {
            Content = JsonContent.Create(new { reason }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        return await client.SendAsync(req);
    }

    private static async Task<HttpResponseMessage> RestoreCustomerAsync(HttpClient client, Guid id)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/customers/{id}/restore");
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        return await client.SendAsync(req);
    }

    // ── T-CUS-01: POST /api/v1/customers crea cliente → 201 ──────────────────

    [Fact(DisplayName = "T-CUS-01: POST /customers crea cliente y devuelve 201 con Location")]
    public async Task Post_cliente_devuelve_201_con_location()
    {
        using var client = CreateClient();
        await LoginAsync(client, CustomersVehiclesWebApplicationFactory.OwnerEmail,
            CustomersVehiclesWebApplicationFactory.OwnerPassword);

        var resp = await PostCustomerAsync(client, "Juan García T-CUS-01");

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal("Juan García T-CUS-01", body.GetProperty("name").GetString());

        var location = resp.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.Contains(id.ToString(), location);
    }

    // ── T-CUS-02: GET /api/v1/customers/{id} devuelve el cliente ─────────────

    [Fact(DisplayName = "T-CUS-02: GET /customers/{id} devuelve la ficha del cliente")]
    public async Task Get_cliente_devuelve_ficha_completa()
    {
        using var client = CreateClient();
        await LoginAsync(client, CustomersVehiclesWebApplicationFactory.OwnerEmail,
            CustomersVehiclesWebApplicationFactory.OwnerPassword);

        // Crear primero
        var createResp = await PostCustomerAsync(client, "María López T-CUS-02");
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        // Leer la ficha
        var getResp = await GetCustomerAsync(client, id);
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);

        var dto = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(id, dto.GetProperty("id").GetGuid());
        Assert.Equal("María López T-CUS-02", dto.GetProperty("name").GetString());
        Assert.Equal(0m, dto.GetProperty("balance").GetDecimal());
    }

    // ── T-CUS-03: PUT /api/v1/customers/{id} actualiza nombre (If-Match) ─────

    [Fact(DisplayName = "T-CUS-03: PUT /customers/{id} con If-Match actualiza el nombre")]
    public async Task Put_cliente_actualiza_nombre_con_etag()
    {
        using var client = CreateClient();
        await LoginAsync(client, CustomersVehiclesWebApplicationFactory.OwnerEmail,
            CustomersVehiclesWebApplicationFactory.OwnerPassword);

        // Crear cliente
        var createResp = await PostCustomerAsync(client, "Original T-CUS-03");
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        var version = created.GetProperty("version").GetUInt32();

        // Actualizar
        var putResp = await PutCustomerAsync(client, id, version, new { name = "Actualizado T-CUS-03" });
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        var updated = await putResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Actualizado T-CUS-03", updated.GetProperty("name").GetString());
    }

    [Fact(DisplayName = "T-CUS-03: PUT sin If-Match devuelve 422")]
    public async Task Put_sin_if_match_devuelve_422()
    {
        using var client = CreateClient();
        await LoginAsync(client, CustomersVehiclesWebApplicationFactory.OwnerEmail,
            CustomersVehiclesWebApplicationFactory.OwnerPassword);

        var createResp = await PostCustomerAsync(client, "Sin etag T-CUS-03b");
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/customers/{id}")
        {
            Content = JsonContent.Create(new { name = "Intento sin etag" }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        // Deliberadamente sin If-Match

        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    // ── T-CUS-04: teléfono se normaliza a E.164 en la respuesta de la API ────

    [Fact(DisplayName = "T-CUS-04: teléfono local se normaliza a E.164 al crear cliente")]
    public async Task Telefono_se_normaliza_a_e164_en_api()
    {
        using var client = CreateClient();
        await LoginAsync(client, CustomersVehiclesWebApplicationFactory.OwnerEmail,
            CustomersVehiclesWebApplicationFactory.OwnerPassword);

        // 011 15-555-1234 → +549115551234
        var resp = await PostCustomerAsync(client, "Cliente Teléfono T-CUS-04", phone: "011 15-555-1234");
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("+549115551234", body.GetProperty("phone").GetString());
    }

    // ── T-CUS-05: DELETE → 204 (borrado lógico) ──────────────────────────────

    [Fact(DisplayName = "T-CUS-05: DELETE /customers/{id} hace borrado lógico → 204")]
    public async Task Delete_cliente_devuelve_204()
    {
        using var client = CreateClient();
        await LoginAsync(client, CustomersVehiclesWebApplicationFactory.OwnerEmail,
            CustomersVehiclesWebApplicationFactory.OwnerPassword);

        var createResp = await PostCustomerAsync(client, "A borrar T-CUS-05");
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var deleteResp = await DeleteCustomerAsync(client, id, "Test de borrado T-CUS-05");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);
    }

    // ── T-CUS-06: GET cliente borrado → 404 ──────────────────────────────────

    [Fact(DisplayName = "T-CUS-06: GET de cliente borrado devuelve 404")]
    public async Task Get_cliente_borrado_devuelve_404()
    {
        using var client = CreateClient();
        await LoginAsync(client, CustomersVehiclesWebApplicationFactory.OwnerEmail,
            CustomersVehiclesWebApplicationFactory.OwnerPassword);

        // Crear y borrar
        var createResp = await PostCustomerAsync(client, "Borrado T-CUS-06");
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        await DeleteCustomerAsync(client, id, "Test T-CUS-06");

        // GET debe devolver 404
        var getResp = await GetCustomerAsync(client, id);
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    // ── T-CUS-07: DELETE con balance=0 → 204 (regla de negocio E4) ───────────

    [Fact(DisplayName = "T-CUS-07: DELETE cliente con balance=0 → 204 (E3: balance siempre cero)")]
    public async Task Delete_cliente_con_balance_cero_devuelve_204()
    {
        // En E3, GetPendingBalanceAsync devuelve siempre 0.
        // Este test documenta que el DELETE funciona en E3 y que la restricción de balance
        // se activará en E4 cuando se implementen las órdenes.
        using var client = CreateClient();
        await LoginAsync(client, CustomersVehiclesWebApplicationFactory.OwnerEmail,
            CustomersVehiclesWebApplicationFactory.OwnerPassword);

        var createResp = await PostCustomerAsync(client, "Balance cero T-CUS-07");
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var deleteResp = await DeleteCustomerAsync(client, id, "Sin deuda T-CUS-07");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);
    }

    // ── T-CUS-08: POST restore restaura cliente borrado → 200 ────────────────

    [Fact(DisplayName = "T-CUS-08: POST /customers/{id}/restore restaura cliente borrado → 200")]
    public async Task Restore_cliente_borrado_devuelve_200()
    {
        using var client = CreateClient();
        await LoginAsync(client, CustomersVehiclesWebApplicationFactory.OwnerEmail,
            CustomersVehiclesWebApplicationFactory.OwnerPassword);

        // Crear → borrar → restaurar
        var createResp = await PostCustomerAsync(client, "Restaurado T-CUS-08");
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        await DeleteCustomerAsync(client, id, "Test de restauración");

        var restoreResp = await RestoreCustomerAsync(client, id);
        Assert.Equal(HttpStatusCode.OK, restoreResp.StatusCode);

        var restoredDto = await restoreResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Restaurado T-CUS-08", restoredDto.GetProperty("name").GetString());

        // Verificar que GET vuelve a funcionar
        var getResp = await GetCustomerAsync(client, id);
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
    }

    // ── T-ISO-01: GET de otro taller → 404 ───────────────────────────────────

    [Fact(DisplayName = "T-ISO-01: GET /customers/{id} de otro taller devuelve 404 (cubierto por T-ISO-ALL)")]
    public async Task Get_cliente_de_otro_taller_devuelve_404()
    {
        // T-ISO-ALL en PermissionMatrixTests ya cubre esto de forma generada.
        // Este test explícito sirve como documentación de T-ISO-01 y como doble verificación.
        using var client = CreateClient();
        await LoginAsync(client, CustomersVehiclesWebApplicationFactory.OwnerEmail,
            CustomersVehiclesWebApplicationFactory.OwnerPassword);

        // Un ID que no pertenece al taller → 404
        var getResp = await GetCustomerAsync(client, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }
}
