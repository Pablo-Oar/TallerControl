using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TallerControl.Api.Identity;
using TallerControl.Api.IntegrationTests.Persistence;

namespace TallerControl.Api.IntegrationTests.Dashboard;

/// <summary>
/// Inicio ampliado (docs/plan-dashboard-inicio.md): T-DASH-03..09. Cada test levanta su propio taller
/// (PostgreSQL real), porque algunos cambian los umbrales.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class DashboardV2EndpointsTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private DashboardV2WebApplicationFactory _factory = null!;

    public DashboardV2EndpointsTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        _factory = await DashboardV2WebApplicationFactory.CreateAsync(_postgres);
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

    private static async Task LoginAsync(HttpClient client)
    {
        var tokenResp = await client.GetAsync("/api/v1/auth/antiforgery-token");
        tokenResp.EnsureSuccessStatusCode();
        var csrf = (await tokenResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new
            {
                email = DashboardV2WebApplicationFactory.OwnerEmail,
                password = DashboardV2WebApplicationFactory.Password,
                rememberMe = false,
            }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(req)).StatusCode);
    }

    private async Task<JsonElement> GetDashboardAsync()
    {
        using var client = CreateClient();
        await LoginAsync(client);
        var resp = await client.GetAsync("/api/v1/dashboard");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact(DisplayName = "T-DASH-03: ingresos de hoy y de los últimos 7 días; el saldo por cobrar descuenta los cobros")]
    public async Task Ingresos_de_hoy_y_ultimos_7_dias()
    {
        var body = await GetDashboardAsync();

        Assert.Equal(400m, body.GetProperty("revenueToday").GetDecimal());
        Assert.Equal(1, body.GetProperty("paymentsToday").GetInt32());

        var days = body.GetProperty("revenueLast7Days").EnumerateArray().ToList();
        Assert.Equal(7, days.Count);
        Assert.Equal(400m, days[^1].GetProperty("amount").GetDecimal());
        Assert.All(days.Take(6), d => Assert.Equal(0m, d.GetProperty("amount").GetDecimal()));

        // 1210 (A) + 500 (B) + (1000 - 400) (C) + 1000 (D); el otro taller no suma.
        Assert.Equal(3310m, body.GetProperty("totalReceivable").GetDecimal());
    }

    [Fact(DisplayName = "T-DASH-04: órdenes por estado, trabadas y «Taller en vivo» (las más antiguas primero)")]
    public async Task Ordenes_por_estado_y_trabadas()
    {
        var body = await GetDashboardAsync();

        var byStatus = body.GetProperty("ordersByStatus");
        Assert.Equal(2, byStatus.GetProperty("pending").GetInt32());
        Assert.Equal(1, byStatus.GetProperty("inProgress").GetInt32());
        Assert.Equal(0, byStatus.GetProperty("waitingParts").GetInt32());
        Assert.Equal(1, byStatus.GetProperty("readyToDeliver").GetInt32());
        // A (10 d), B (45 d) y D (5 d en «completada»); C se movió hoy.
        Assert.Equal(3, byStatus.GetProperty("stuck").GetInt32());

        var live = body.GetProperty("liveOrders").EnumerateArray().ToList();
        Assert.Equal(4, live.Count);
        Assert.Equal(2, live[0].GetProperty("number").GetInt32()); // B: la más antigua
        Assert.True(live[0].GetProperty("isStuck").GetBoolean());
        Assert.Equal(45, live[0].GetProperty("daysInStatus").GetInt32());
        Assert.False(live[^1].GetProperty("isStuck").GetBoolean()); // C: reciente, al final
        Assert.DoesNotContain(live, o => o.GetProperty("number").GetInt32() == DashboardV2WebApplicationFactory.ForeignOrderNumber);
    }

    [Fact(DisplayName = "T-DASH-05: saldo antiguo = órdenes con saldo desde hace más de N días")]
    public async Task Saldo_antiguo()
    {
        var body = await GetDashboardAsync();

        var aged = body.GetProperty("agedReceivable");
        Assert.Equal(1, aged.GetProperty("count").GetInt32());
        Assert.Equal(500m, aged.GetProperty("amount").GetDecimal()); // solo B (45 d); A tiene 10 d
    }

    [Fact(DisplayName = "T-DASH-06: presupuestos calientes (más de 48 h sin respuesta) y enviados")]
    public async Task Presupuestos_calientes()
    {
        var body = await GetDashboardAsync();

        Assert.Equal(2, body.GetProperty("sentQuotes").GetInt32());
        Assert.Equal(2420m, body.GetProperty("sentQuotesTotal").GetDecimal());

        var hot = body.GetProperty("hotQuotes");
        Assert.Equal(1, hot.GetProperty("total").GetInt32()); // el de hace 1 hora no es caliente
        var item = hot.GetProperty("items").EnumerateArray().Single();
        Assert.Equal(1, item.GetProperty("number").GetInt32());
        Assert.Equal("DASHV2", item.GetProperty("vehiclePlate").GetString());
        Assert.True(item.GetProperty("hoursSinceSent").GetInt32() >= 71);
    }

    [Fact(DisplayName = "T-DASH-07: turnos de hoy, caja con sesión abierta y stock bajo")]
    public async Task Turnos_caja_y_stock()
    {
        var body = await GetDashboardAsync();

        var appointments = body.GetProperty("todayAppointments");
        Assert.Equal(1, appointments.GetProperty("total").GetInt32());
        Assert.Equal(1, appointments.GetProperty("unassigned").GetInt32());

        var cash = body.GetProperty("cash");
        Assert.Equal(400m, cash.GetProperty("totalBalance").GetDecimal());
        var account = cash.GetProperty("accounts").EnumerateArray().Single();
        Assert.Equal("Caja del mostrador", account.GetProperty("name").GetString());
        Assert.Equal(DashboardV2WebApplicationFactory.OwnerName, account.GetProperty("sessionOpenedBy").GetString());

        var stock = body.GetProperty("lowStock").EnumerateArray().Single();
        Assert.Equal("Filtro de aceite", stock.GetProperty("name").GetString());
        Assert.Equal(1, body.GetProperty("lowStockItems").GetInt32());
    }

    [Fact(DisplayName = "T-DASH-08: los umbrales del taller cambian lo que se marca como atrasado")]
    public async Task Los_umbrales_del_taller_se_respetan()
    {
        var defaults = await GetDashboardAsync();
        var thresholds = defaults.GetProperty("thresholds");
        Assert.Equal(48, thresholds.GetProperty("hotQuoteHours").GetInt32());
        Assert.Equal(3, thresholds.GetProperty("stuckOrderDays").GetInt32());
        Assert.Equal(30, thresholds.GetProperty("agedReceivableDays").GetInt32());

        // Con 60 días nada de lo sembrado está trabado ni tiene saldo antiguo, y con 720 h no hay presupuestos calientes.
        await _factory.SetThresholdsAsync(hotQuoteHours: 720, stuckOrderDays: 60, agedReceivableDays: 60);

        var body = await GetDashboardAsync();
        Assert.Equal(0, body.GetProperty("ordersByStatus").GetProperty("stuck").GetInt32());
        Assert.Equal(0, body.GetProperty("agedReceivable").GetProperty("count").GetInt32());
        Assert.Equal(0, body.GetProperty("hotQuotes").GetProperty("total").GetInt32());
        Assert.Equal(60, body.GetProperty("thresholds").GetProperty("stuckOrderDays").GetInt32());
    }

    [Fact(DisplayName = "T-DASH-09: sin sesión el Inicio responde 401")]
    public async Task Sin_sesion_responde_401()
    {
        using var client = CreateClient();

        var resp = await client.GetAsync("/api/v1/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
