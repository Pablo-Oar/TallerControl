using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TallerControl.Api.Identity;
using TallerControl.Api.IntegrationTests.Persistence;

namespace TallerControl.Api.IntegrationTests.Cash;

/// <summary>
/// E11: tests de integración de caja.
/// T-CAJ-01..04 (doc 08, REQ-E11, D-16).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CashEndpointsTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private CashWebApplicationFactory _factory = null!;

    public CashEndpointsTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        _factory = await CashWebApplicationFactory.CreateAsync(_postgres);
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

    private static async Task<HttpResponseMessage> PostCashAccountAsync(
        HttpClient client, string name, string kind = "cash")
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/cash/accounts")
        {
            Content = JsonContent.Create(new { name, kind }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        return await client.SendAsync(req);
    }

    private static async Task<HttpResponseMessage> PostOpenSessionAsync(
        HttpClient client, Guid accountId, string? note = null)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/cash/accounts/{accountId}/open")
        {
            Content = JsonContent.Create(new { openingNote = note }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        return await client.SendAsync(req);
    }

    private static async Task<HttpResponseMessage> PostCloseSessionAsync(
        HttpClient client, Guid accountId, string? note = null)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/cash/accounts/{accountId}/close")
        {
            Content = JsonContent.Create(new { closingNote = note }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        return await client.SendAsync(req);
    }

    private static async Task<HttpResponseMessage> PostManualMovementAsync(
        HttpClient client, Guid accountId, decimal amount, string direction, string note)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/cash/accounts/{accountId}/movements")
        {
            Content = JsonContent.Create(new { amount, direction, note }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        return await client.SendAsync(req);
    }

    // ── T-CAJ-01: POST /cash/accounts crea cuenta → 201 ─────────────────────

    [Fact(DisplayName = "T-CAJ-01: POST /cash/accounts crea cuenta y devuelve 201 con Location")]
    public async Task Post_cuenta_devuelve_201_con_location()
    {
        using var client = CreateClient();
        await LoginAsync(client, CashWebApplicationFactory.OwnerEmail,
            CashWebApplicationFactory.OwnerPassword);

        var resp = await PostCashAccountAsync(client, "Caja Principal T-CAJ-01");

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal("Caja Principal T-CAJ-01", body.GetProperty("name").GetString());
        Assert.True(body.GetProperty("isActive").GetBoolean());

        var location = resp.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.Contains(id.ToString(), location);
    }

    // ── T-CAJ-01: GET /cash/accounts/{id} devuelve detalle ───────────────────

    [Fact(DisplayName = "T-CAJ-01: GET /cash/accounts/{id} devuelve el detalle de la cuenta")]
    public async Task Get_cuenta_devuelve_detalle()
    {
        using var client = CreateClient();
        await LoginAsync(client, CashWebApplicationFactory.OwnerEmail,
            CashWebApplicationFactory.OwnerPassword);

        var createResp = await PostCashAccountAsync(client, "Caja GET T-CAJ-01b");
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var getResp = await client.GetAsync($"/api/v1/cash/accounts/{id}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);

        var dto = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(id, dto.GetProperty("id").GetGuid());
        Assert.Equal("Caja GET T-CAJ-01b", dto.GetProperty("name").GetString());
    }

    // ── F-02: cierre con efectivo contado y saldo en el listado ──────────────

    [Fact(DisplayName = "T-CAJ-05: El listado informa saldo y sesión abierta; cerrar con efectivo contado registra la diferencia")]
    public async Task Cierre_con_contado_registra_esperado_y_diferencia()
    {
        using var client = CreateClient();
        await LoginAsync(client, CashWebApplicationFactory.OwnerEmail, CashWebApplicationFactory.OwnerPassword);

        var account = await (await PostCashAccountAsync(client, "Caja T-CAJ-05")).Content.ReadFromJsonAsync<JsonElement>();
        var accountId = account.GetProperty("id").GetGuid();
        (await PostOpenSessionAsync(client, accountId, "Apertura")).EnsureSuccessStatusCode();
        (await PostManualMovementAsync(client, accountId, 100m, "in", "Fondo inicial")).EnsureSuccessStatusCode();

        var accounts = await client.GetFromJsonAsync<JsonElement>("/api/v1/cash/accounts");
        var row = accounts.EnumerateArray().First(a => a.GetProperty("id").GetGuid() == accountId);
        Assert.Equal(100m, row.GetProperty("balance").GetDecimal());
        Assert.NotEqual(JsonValueKind.Null, row.GetProperty("sessionOpenedAt").ValueKind);

        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/cash/accounts/{accountId}/close")
        {
            Content = JsonContent.Create(new { closingNote = "Cierre del día", countedAmount = 90m }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var note = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("closingNote").GetString()!;
        Assert.Contains("Cierre del día", note);
        Assert.Contains("Esperado 100.00", note);
        Assert.Contains("Diferencia -10.00", note);
    }

    // ── T-CAJ-02: mecánico no puede crear ni manejar cuentas ────────────────

    [Fact(DisplayName = "T-CAJ-02: mecánico recibe 403 al intentar crear una cuenta de caja")]
    public async Task Mecanico_no_puede_crear_cuenta()
    {
        using var client = CreateClient();
        await LoginAsync(client, CashWebApplicationFactory.MechanicEmail,
            CashWebApplicationFactory.MechanicPassword);

        var resp = await PostCashAccountAsync(client, "No autorizado T-CAJ-02");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact(DisplayName = "T-CAJ-02: mecánico recibe 403 al consultar lista de cuentas")]
    public async Task Mecanico_no_puede_listar_cuentas()
    {
        using var client = CreateClient();
        await LoginAsync(client, CashWebApplicationFactory.MechanicEmail,
            CashWebApplicationFactory.MechanicPassword);

        var resp = await client.GetAsync("/api/v1/cash/accounts");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ── T-CAJ-03: ciclo abrir → cerrar sesión ────────────────────────────────

    [Fact(DisplayName = "T-CAJ-03: POST /open abre sesión y POST /close la cierra")]
    public async Task Ciclo_abrir_cerrar_sesion()
    {
        using var client = CreateClient();
        await LoginAsync(client, CashWebApplicationFactory.OwnerEmail,
            CashWebApplicationFactory.OwnerPassword);

        var createResp = await PostCashAccountAsync(client, "Caja Sesión T-CAJ-03");
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var accountId = created.GetProperty("id").GetGuid();

        // Abrir sesión
        var openResp = await PostOpenSessionAsync(client, accountId, "Apertura del día");
        Assert.Equal(HttpStatusCode.OK, openResp.StatusCode);

        var session = await openResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("open", session.GetProperty("status").GetString(), StringComparer.OrdinalIgnoreCase);

        // Cerrar sesión
        var closeResp = await PostCloseSessionAsync(client, accountId, "Cierre del día");
        Assert.Equal(HttpStatusCode.OK, closeResp.StatusCode);

        var closedSession = await closeResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("closed", closedSession.GetProperty("status").GetString(), StringComparer.OrdinalIgnoreCase);
    }

    // ── T-CAJ-04: movimiento manual de caja ──────────────────────────────────

    [Fact(DisplayName = "T-CAJ-04: POST /movements crea movimiento manual y devuelve 201")]
    public async Task Movimiento_manual_devuelve_201()
    {
        using var client = CreateClient();
        await LoginAsync(client, CashWebApplicationFactory.OwnerEmail,
            CashWebApplicationFactory.OwnerPassword);

        var createResp = await PostCashAccountAsync(client, "Caja Movimiento T-CAJ-04");
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var accountId = created.GetProperty("id").GetGuid();

        // Abrir sesión antes de mover
        await PostOpenSessionAsync(client, accountId);

        var movResp = await PostManualMovementAsync(client, accountId, 500m, "in", "Ingreso inicial de caja");
        Assert.Equal(HttpStatusCode.Created, movResp.StatusCode);

        var mov = await movResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(500m, mov.GetProperty("amount").GetDecimal());
        Assert.Equal("in", mov.GetProperty("direction").GetString(), StringComparer.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "T-CAJ-04: GET /movements devuelve historial de movimientos")]
    public async Task Get_movimientos_devuelve_historial()
    {
        using var client = CreateClient();
        await LoginAsync(client, CashWebApplicationFactory.OwnerEmail,
            CashWebApplicationFactory.OwnerPassword);

        var createResp = await PostCashAccountAsync(client, "Caja Historial T-CAJ-04b");
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var accountId = created.GetProperty("id").GetGuid();

        await PostOpenSessionAsync(client, accountId);
        await PostManualMovementAsync(client, accountId, 200m, "in", "Primera entrada");
        await PostManualMovementAsync(client, accountId, 50m, "out", "Primera salida");

        var movResp = await client.GetAsync($"/api/v1/cash/accounts/{accountId}/movements");
        Assert.Equal(HttpStatusCode.OK, movResp.StatusCode);

        var movements = await movResp.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(movements);
        Assert.Equal(2, movements.Length);
    }
}
