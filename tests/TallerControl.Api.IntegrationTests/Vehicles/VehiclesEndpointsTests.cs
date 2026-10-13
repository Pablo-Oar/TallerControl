using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TallerControl.Api.Identity;
using TallerControl.Api.IntegrationTests.Customers;
using TallerControl.Api.IntegrationTests.Persistence;

namespace TallerControl.Api.IntegrationTests.Vehicles;

/// <summary>
/// E3 bloque 2: tests de integración de vehículos.
/// T-VEH-01..09 (doc 08 §4, doc 05 §2.3, REQ-06).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class VehiclesEndpointsTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private CustomersVehiclesWebApplicationFactory _factory = null!;

    public VehiclesEndpointsTests(PostgresFixture postgres)
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

    /// <summary>Crea un cliente de soporte para los tests de vehículos. Retorna el Id del cliente.</summary>
    private static async Task<Guid> CreateSupportCustomerAsync(HttpClient client, string nameSuffix)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customers")
        {
            Content = JsonContent.Create(new { name = $"Cliente Soporte {nameSuffix}" }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        var resp = await client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<HttpResponseMessage> PostVehicleAsync(
        HttpClient client,
        string plate,
        Guid customerId,
        int odometerKm = 0,
        string? brand = null)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/vehicles")
        {
            Content = JsonContent.Create(new { plate, customerId, odometerKm, brand }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        return await client.SendAsync(req);
    }

    private static async Task<HttpResponseMessage> GetVehicleAsync(HttpClient client, Guid id)
    {
        return await client.GetAsync($"/api/v1/vehicles/{id}");
    }

    private static async Task<HttpResponseMessage> PutVehicleAsync(
        HttpClient client,
        Guid id,
        uint version,
        string plate,
        Guid customerId,
        int odometerKm,
        string? brand = null)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/vehicles/{id}")
        {
            Content = JsonContent.Create(new { plate, customerId, odometerKm, brand }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        req.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        return await client.SendAsync(req);
    }

    private static async Task<HttpResponseMessage> DeleteVehicleAsync(
        HttpClient client,
        Guid id,
        string reason = "Test: borrado lógico")
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/vehicles/{id}")
        {
            Content = JsonContent.Create(new { reason }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        return await client.SendAsync(req);
    }

    // ── T-VEH-01: POST /api/v1/vehicles crea vehículo → 201 ─────────────────

    [Fact(DisplayName = "T-VEH-01: POST /vehicles crea vehículo y devuelve 201 con Location")]
    public async Task Post_vehiculo_devuelve_201_con_location()
    {
        using var client = CreateClient();
        await LoginAsync(client, CustomersVehiclesWebApplicationFactory.OwnerEmail,
            CustomersVehiclesWebApplicationFactory.OwnerPassword);

        var customerId = await CreateSupportCustomerAsync(client, "T-VEH-01");

        var resp = await PostVehicleAsync(client, "TVH001", customerId, brand: "Toyota");

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal("TVH001", body.GetProperty("plate").GetString());

        var location = resp.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.Contains(id.ToString(), location);
    }

    // ── T-VEH-02: patente se normaliza al crear ───────────────────────────────

    [Fact(DisplayName = "T-VEH-02: patente en minúsculas se normaliza a mayúsculas en la respuesta")]
    public async Task Patente_se_normaliza_al_crear_vehiculo()
    {
        using var client = CreateClient();
        await LoginAsync(client, CustomersVehiclesWebApplicationFactory.OwnerEmail,
            CustomersVehiclesWebApplicationFactory.OwnerPassword);

        var customerId = await CreateSupportCustomerAsync(client, "T-VEH-02");

        // Enviar con espacios/minúsculas → debe normalizarse
        var resp = await PostVehicleAsync(client, "abc 123", customerId);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ABC123", body.GetProperty("plate").GetString());
    }

    // ── T-VEH-03: GET /api/v1/vehicles/{id} devuelve el vehículo ─────────────

    [Fact(DisplayName = "T-VEH-03: GET /vehicles/{id} devuelve la ficha del vehículo")]
    public async Task Get_vehiculo_devuelve_ficha_completa()
    {
        using var client = CreateClient();
        await LoginAsync(client, CustomersVehiclesWebApplicationFactory.OwnerEmail,
            CustomersVehiclesWebApplicationFactory.OwnerPassword);

        var customerId = await CreateSupportCustomerAsync(client, "T-VEH-03");
        var createResp = await PostVehicleAsync(client, "TVH003", customerId, brand: "Ford");
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var getResp = await GetVehicleAsync(client, id);
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);

        var dto = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(id, dto.GetProperty("id").GetGuid());
        Assert.Equal("TVH003", dto.GetProperty("plate").GetString());
        Assert.Equal("Ford", dto.GetProperty("brand").GetString());
        Assert.Equal(customerId, dto.GetProperty("customerId").GetGuid());
    }

    // ── T-VEH-04: PUT /api/v1/vehicles/{id} actualiza marca (If-Match) ───────

    [Fact(DisplayName = "T-VEH-04: PUT /vehicles/{id} con If-Match actualiza la marca")]
    public async Task Put_vehiculo_actualiza_marca_con_etag()
    {
        using var client = CreateClient();
        await LoginAsync(client, CustomersVehiclesWebApplicationFactory.OwnerEmail,
            CustomersVehiclesWebApplicationFactory.OwnerPassword);

        var customerId = await CreateSupportCustomerAsync(client, "T-VEH-04");
        var createResp = await PostVehicleAsync(client, "TVH004", customerId, brand: "Fiat");
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        var version = created.GetProperty("version").GetUInt32();

        var putResp = await PutVehicleAsync(client, id, version, "TVH004", customerId, 0, brand: "Renault");
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        var updated = await putResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Renault", updated.GetProperty("brand").GetString());
    }

    // ── T-VEH-05: VehicleDto.ActiveQrCard es null en E3 ──────────────────────

    [Fact(DisplayName = "T-VEH-05: GET /vehicles/{id} devuelve activeQrCard nulo (E3, QR aún no implementado)")]
    public async Task ActiveQrCard_es_nulo_en_e3()
    {
        using var client = CreateClient();
        await LoginAsync(client, CustomersVehiclesWebApplicationFactory.OwnerEmail,
            CustomersVehiclesWebApplicationFactory.OwnerPassword);

        var customerId = await CreateSupportCustomerAsync(client, "T-VEH-05");
        var createResp = await PostVehicleAsync(client, "TVH005", customerId);
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var getResp = await GetVehicleAsync(client, id);
        var dto = await getResp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(JsonValueKind.Null, dto.GetProperty("activeQrCard").ValueKind);
    }

    // ── T-VEH-06: PUT con km < actual, sin vehicles.odometer.correct → 422 ───

    [Fact(DisplayName = "T-VEH-06: PUT con km menor al actual, rol sin odometer.correct → 422")]
    public async Task Put_km_menor_sin_permiso_devuelve_422()
    {
        // Owner crea el vehículo (con km inicial)
        using var ownerClient = CreateClient();
        await LoginAsync(ownerClient, CustomersVehiclesWebApplicationFactory.OwnerEmail,
            CustomersVehiclesWebApplicationFactory.OwnerPassword);

        var customerId = await CreateSupportCustomerAsync(ownerClient, "T-VEH-06");
        var createResp = await PostVehicleAsync(ownerClient, "TVH006", customerId, odometerKm: 400);
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        var version = created.GetProperty("version").GetUInt32();

        // AdminStaff intenta bajar el km (no tiene vehicles.odometer.correct)
        using var adminClient = CreateClient();
        await LoginAsync(adminClient, CustomersVehiclesWebApplicationFactory.AdminEmail,
            CustomersVehiclesWebApplicationFactory.AdminPassword);

        var putResp = await PutVehicleAsync(adminClient, id, version, "TVH006", customerId, odometerKm: 300);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, putResp.StatusCode);
    }

    // ── T-VEH-07: PUT con km < actual, con vehicles.odometer.correct → 200 ───

    [Fact(DisplayName = "T-VEH-07: PUT con km menor al actual, Owner (con odometer.correct) → 200")]
    public async Task Put_km_menor_con_permiso_devuelve_200()
    {
        using var client = CreateClient();
        await LoginAsync(client, CustomersVehiclesWebApplicationFactory.OwnerEmail,
            CustomersVehiclesWebApplicationFactory.OwnerPassword);

        var customerId = await CreateSupportCustomerAsync(client, "T-VEH-07");
        var createResp = await PostVehicleAsync(client, "TVH007", customerId, odometerKm: 400);
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        var version = created.GetProperty("version").GetUInt32();

        // Owner tiene vehicles.odometer.correct → puede bajar el km
        var putResp = await PutVehicleAsync(client, id, version, "TVH007", customerId, odometerKm: 300);
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        var updated = await putResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(300, updated.GetProperty("odometerKm").GetInt32());
    }

    // ── T-VEH-08: DELETE /api/v1/vehicles/{id} → 204 ────────────────────────

    [Fact(DisplayName = "T-VEH-08: DELETE /vehicles/{id} hace borrado lógico → 204")]
    public async Task Delete_vehiculo_devuelve_204()
    {
        using var client = CreateClient();
        await LoginAsync(client, CustomersVehiclesWebApplicationFactory.OwnerEmail,
            CustomersVehiclesWebApplicationFactory.OwnerPassword);

        var customerId = await CreateSupportCustomerAsync(client, "T-VEH-08");
        var createResp = await PostVehicleAsync(client, "TVH008", customerId);
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var deleteResp = await DeleteVehicleAsync(client, id, "Test borrado T-VEH-08");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        // Verificar que ya no es visible
        var getResp = await GetVehicleAsync(client, id);
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    // ── T-VEH-09: DELETE vehículo sin órdenes abiertas → 204 ─────────────────

    [Fact(DisplayName = "T-VEH-09: DELETE vehículo sin órdenes abiertas → 204 (E3: HasOpenWorkOrders siempre false)")]
    public async Task Delete_vehiculo_sin_ordenes_abiertas_devuelve_204()
    {
        // En E3, HasOpenWorkOrdersAsync devuelve siempre false.
        // Este test documenta que el DELETE funciona y que la restricción de órdenes
        // se activará en E4.
        using var client = CreateClient();
        await LoginAsync(client, CustomersVehiclesWebApplicationFactory.OwnerEmail,
            CustomersVehiclesWebApplicationFactory.OwnerPassword);

        var customerId = await CreateSupportCustomerAsync(client, "T-VEH-09");
        var createResp = await PostVehicleAsync(client, "TVH009", customerId);
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var deleteResp = await DeleteVehicleAsync(client, id, "Test T-VEH-09");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);
    }
}
