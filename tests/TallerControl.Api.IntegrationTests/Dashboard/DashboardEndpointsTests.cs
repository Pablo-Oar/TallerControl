using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TallerControl.Api.Identity;
using TallerControl.Api.IntegrationTests.Persistence;

namespace TallerControl.Api.IntegrationTests.Dashboard;

/// <summary>
/// E9 bloque 4: tests del endpoint GET /api/v1/dashboard.
/// T-DASH-01 (orden cancelada excluida del KPI) y T-DASH-02 (coherencia totalReceivable).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class DashboardEndpointsTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private DashboardWebApplicationFactory _factory = null!;

    public DashboardEndpointsTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        _factory = await DashboardWebApplicationFactory.CreateAsync(_postgres);
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

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

    // ── T-DASH-01 ─────────────────────────────────────────────────────────────

    [Fact(DisplayName = "T-DASH-01: OT cancelada con saldo no suma a totalReceivable")]
    public async Task OT_cancelada_excluida_del_totalReceivable()
    {
        var client = CreateClient();
        await LoginAsync(client, DashboardWebApplicationFactory.OwnerEmail, DashboardWebApplicationFactory.Password);

        var resp = await client.GetAsync("/api/v1/dashboard");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        // La OT cancelada (total 2420) NO debe sumarse. Solo las dos OTs abiertas.
        var totalReceivable = body.GetProperty("totalReceivable").GetDecimal();
        Assert.Equal(_factory.ExpectedTotalReceivable, totalReceivable);
    }

    // ── T-DASH-02 ─────────────────────────────────────────────────────────────

    [Fact(DisplayName = "T-DASH-02: totalReceivable coincide con la suma de balances de las OTs")]
    public async Task TotalReceivable_coincide_con_suma_por_OT()
    {
        var client = CreateClient();
        await LoginAsync(client, DashboardWebApplicationFactory.OwnerEmail, DashboardWebApplicationFactory.Password);

        var dashResp = await client.GetAsync("/api/v1/dashboard");
        Assert.Equal(HttpStatusCode.OK, dashResp.StatusCode);
        var dash = await dashResp.Content.ReadFromJsonAsync<JsonElement>();
        var totalReceivable = dash.GetProperty("totalReceivable").GetDecimal();

        // El dashboard debe tener openWorkOrders = 2 (wo1 + wo2, wo3 cancelada no cuenta)
        var openWorkOrders = dash.GetProperty("openWorkOrders").GetInt32();
        Assert.Equal(2, openWorkOrders);

        // El total del dashboard debe ser la suma esperada sembrada en la factory
        Assert.Equal(_factory.ExpectedTotalReceivable, totalReceivable);
        Assert.True(totalReceivable > 0, "totalReceivable debe ser positivo cuando hay OTs con saldo");
    }

    // ── Sin sesión → 401 ──────────────────────────────────────────────────────

    [Fact(DisplayName = "GET /dashboard sin sesión devuelve 401")]
    public async Task Sin_sesion_devuelve_401()
    {
        var client = CreateClient();
        var resp = await client.GetAsync("/api/v1/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
