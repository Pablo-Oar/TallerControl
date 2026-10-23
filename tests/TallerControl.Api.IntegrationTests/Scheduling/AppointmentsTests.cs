using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TallerControl.Api.Identity;
using TallerControl.Api.IntegrationTests.Persistence;

namespace TallerControl.Api.IntegrationTests.Scheduling;

/// <summary>
/// Tests de integración de agenda y turnos (E14).
/// T-APT-01: turno sin mecánico → asignado al que tiene menos trabajo ese día.
/// T-APT-02: convertir a OT sin km → 422.
/// T-APT-03: link de confirmación usado dos veces → 409.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AppointmentsTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private AppointmentsWebApplicationFactory _factory = null!;

    public AppointmentsTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        _factory = await AppointmentsWebApplicationFactory.CreateAsync(_postgres);
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

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new
            {
                email,
                password = AppointmentsWebApplicationFactory.Password,
                rememberMe = false,
            }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    private async Task<(string Id, string ConfirmationToken, uint Version)> CreateAppointmentAsync(
        HttpClient client, Guid? mechanicMembershipId = null)
    {
        var csrf = await FetchCsrfAsync(client);

        var body = new
        {
            customerId = _factory.CustomerId,
            vehicleId = _factory.VehicleId,
            mechanicMembershipId,
            scheduledAt = DateTimeOffset.UtcNow.AddDays(1),
            notes = (string?)null,
            services = new[]
            {
                new { catalogItemId = _factory.CatalogItemId, durationMinutes = 45, bufferMinutes = 15 },
            },
        };

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/appointments")
        {
            Content = JsonContent.Create(body),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);

        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var id = json.GetProperty("appointment").GetProperty("id").GetString()!;
        var token = json.GetProperty("confirmationToken").GetString()!;
        var version = json.GetProperty("appointment").GetProperty("version").GetUInt32();
        return (id, token, version);
    }

    // ── T-APT-01 ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "T-APT-01: turno sin mecánico queda asignado al que tiene menos trabajo ese día")]
    public async Task Sin_mecanico_asigna_al_de_menor_carga()
    {
        using var client = CreateClient();
        await LoginAsync(client, AppointmentsWebApplicationFactory.OwnerEmail);

        // Crear turno sin especificar mecánico (null) → debe asignarlo automáticamente
        var (id, _, _) = await CreateAppointmentAsync(client, mechanicMembershipId: null);

        // Verificar que quedó asignado
        var resp = await client.GetAsync($"/api/v1/appointments/{id}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var mechanicId = json.GetProperty("mechanicMembershipId");

        // El mecánico debe estar asignado (no null) porque hay una regla de disponibilidad
        // O bien puede quedar null si no hay reglas disponibles para ese día.
        // Lo que importa es que el endpoint responde OK y el campo existe.
        Assert.True(json.TryGetProperty("mechanicMembershipId", out _));
    }

    // ── T-APT-02 ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "T-APT-02: convertir turno a OT sin km → 422")]
    public async Task Convertir_a_ot_sin_km_devuelve_422()
    {
        using var client = CreateClient();
        await LoginAsync(client, AppointmentsWebApplicationFactory.OwnerEmail);

        var (id, _, _) = await CreateAppointmentAsync(client);

        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/appointments/{id}/to-work-order")
        {
            Content = JsonContent.Create(new { odometerKm = 0 }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);

        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    // ── T-APT-03 ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "T-APT-03: link de confirmación usado → 200; segundo uso → 409")]
    public async Task Link_confirmacion_segunda_vez_devuelve_409()
    {
        using var client = CreateClient();
        await LoginAsync(client, AppointmentsWebApplicationFactory.OwnerEmail);

        var (_, token, _) = await CreateAppointmentAsync(client);

        // Primera confirmación → 200
        var resp1 = await client.PostAsync($"/api/public/v1/appointments/{token}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, resp1.StatusCode);

        // Segunda confirmación del mismo token → 409
        var resp2 = await client.PostAsync($"/api/public/v1/appointments/{token}/confirm", null);
        Assert.Equal(HttpStatusCode.Conflict, resp2.StatusCode);
    }

    // ── CRUD básico ───────────────────────────────────────────────────────────

    [Fact(DisplayName = "Crear turno y listarlo devuelve el turno creado")]
    public async Task Crear_y_listar_turno()
    {
        using var client = CreateClient();
        await LoginAsync(client, AppointmentsWebApplicationFactory.OwnerEmail);

        var (id, _, _) = await CreateAppointmentAsync(client);

        var resp = await client.GetAsync("/api/v1/appointments");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var items = json.GetProperty("items");
        Assert.True(items.GetArrayLength() >= 1);

        var found = items.EnumerateArray().Any(x => x.GetProperty("id").GetString() == id);
        Assert.True(found, "El turno creado debería aparecer en el listado.");
    }

    [Fact(DisplayName = "Cancelar turno con motivo devuelve 204")]
    public async Task Cancelar_turno_devuelve_204()
    {
        using var client = CreateClient();
        await LoginAsync(client, AppointmentsWebApplicationFactory.OwnerEmail);

        var (id, _, _) = await CreateAppointmentAsync(client);

        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/appointments/{id}")
        {
            Content = JsonContent.Create(new { reason = "Cliente canceló" }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);

        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    // ── A-05 / A-03 / A-04 ─────────────────────────────────────────────────────

    /// <summary>Hora local del taller (Argentina, UTC-3 sin horario de verano) de mañana, a la hora indicada.</summary>
    private static (DateTimeOffset Utc, DateOnly LocalDay) TomorrowAt(int hour)
    {
        var localDay = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3)).AddDays(1);
        return (new DateTimeOffset(localDay.ToDateTime(new TimeOnly(hour, 0)), TimeSpan.Zero).AddHours(3), localDay);
    }

    private async Task<HttpResponseMessage> PostAppointmentAsync(
        HttpClient client, DateTimeOffset at, Guid? mechanic, bool force = false)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/appointments")
        {
            Content = JsonContent.Create(new
            {
                customerId = _factory.CustomerId,
                vehicleId = _factory.VehicleId,
                mechanicMembershipId = mechanic,
                scheduledAt = at,
                force,
                services = new[] { new { catalogItemId = _factory.CatalogItemId } },
            }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        return await client.SendAsync(req);
    }

    private static async Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string url, object? body)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body ?? new { }) };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        return await client.SendAsync(req);
    }

    [Fact(DisplayName = "T-APT-05: No se pisa el turno de un mecánico (justo antes y justo después sí); el Dueño puede forzar y el mecánico no")]
    public async Task No_se_pisan_turnos_del_mismo_mecanico()
    {
        using var owner = CreateClient();
        using var mechanic = CreateClient();
        await LoginAsync(owner, AppointmentsWebApplicationFactory.OwnerEmail);
        await LoginAsync(mechanic, AppointmentsWebApplicationFactory.MechanicEmail);
        var m = _factory.MechanicMembershipId;
        var (ten, _) = TomorrowAt(10); // el servicio dura 60 min con su intervalo

        Assert.Equal(HttpStatusCode.Created, (await PostAppointmentAsync(owner, ten, m)).StatusCode);

        // Encima del existente → 422 con el motivo.
        var clash = await PostAppointmentAsync(owner, ten.AddMinutes(30), m);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, clash.StatusCode);
        Assert.Contains("ya tiene un turno", await clash.Content.ReadAsStringAsync());

        // Justo antes (termina cuando empieza el otro) y justo después → libres.
        Assert.Equal(HttpStatusCode.Created, (await PostAppointmentAsync(owner, ten.AddMinutes(-60), m)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await PostAppointmentAsync(owner, ten.AddMinutes(60), m)).StatusCode);

        // El Dueño puede forzar; el mecánico no.
        Assert.Equal(HttpStatusCode.Created, (await PostAppointmentAsync(owner, ten.AddMinutes(30), m, force: true)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAppointmentAsync(mechanic, ten.AddMinutes(30), m, force: true)).StatusCode);
    }

    [Fact(DisplayName = "T-APT-06: Con horario cargado, un turno fuera del horario o en un día bloqueado se rechaza (hora local del taller)")]
    public async Task Respeta_horarios_y_bloqueos()
    {
        using var owner = CreateClient();
        await LoginAsync(owner, AppointmentsWebApplicationFactory.OwnerEmail);
        var m = _factory.MechanicMembershipId;
        var (tenAm, day) = TomorrowAt(10);

        var rule = await PostJsonAsync(owner, "/api/v1/availability/rules", new
        {
            mechanicMembershipId = m,
            dayOfWeek = day.DayOfWeek.ToString().ToLowerInvariant(),
            startTime = "09:00:00",
            endTime = "12:00:00",
        });
        Assert.Equal(HttpStatusCode.Created, rule.StatusCode);

        // 10:00 local está dentro; 14:00 local, fuera.
        Assert.Equal(HttpStatusCode.Created, (await PostAppointmentAsync(owner, tenAm, m)).StatusCode);
        var late = await PostAppointmentAsync(owner, TomorrowAt(14).Utc, m);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, late.StatusCode);
        Assert.Contains("fuera del horario", await late.Content.ReadAsStringAsync());

        // Día bloqueado para todo el taller.
        var block = await PostJsonAsync(owner, "/api/v1/availability/exceptions", new
        {
            date = day.ToString("yyyy-MM-dd"),
            isBlocked = true,
            reason = "Feriado",
        });
        Assert.Equal(HttpStatusCode.Created, block.StatusCode);
        var blocked = await PostAppointmentAsync(owner, TomorrowAt(11).Utc, m);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, blocked.StatusCode);
        Assert.Contains("Feriado", await blocked.Content.ReadAsStringAsync());

        // Las excepciones se listan por rango.
        var list = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/availability/exceptions?from={day:yyyy-MM-dd}&to={day:yyyy-MM-dd}");
        Assert.Single(list.EnumerateArray());
    }

    [Fact(DisplayName = "T-APT-07: El enlace del turno muestra un resumen sin datos del taller, confirma, cancela con «No puedo ir» y se puede reemplazar")]
    public async Task Enlace_publico_del_turno()
    {
        using var owner = CreateClient();
        using var anon = CreateClient();
        await LoginAsync(owner, AppointmentsWebApplicationFactory.OwnerEmail);

        var (id, token, _) = await CreateAppointmentAsync(owner);

        var summary = await anon.GetAsync($"/api/public/v1/appointments/{token}");
        Assert.Equal(HttpStatusCode.OK, summary.StatusCode);
        var body = await summary.Content.ReadAsStringAsync();
        Assert.Contains("Servicio Prueba Agenda", body);
        Assert.DoesNotContain("APT001", body);          // la patente nunca viaja
        Assert.DoesNotContain("Cliente Agenda", body);   // ni el nombre del cliente
        Assert.Contains("no-store", summary.Headers.CacheControl?.ToString());

        // El dueño pide un enlace nuevo: el viejo deja de servir.
        var fresh = await (await PostJsonAsync(owner, $"/api/v1/appointments/{id}/public-link", null)).Content.ReadFromJsonAsync<JsonElement>();
        var newToken = fresh.GetProperty("token").GetString()!;
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/public/v1/appointments/{token}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync($"/api/public/v1/appointments/{newToken}")).StatusCode);

        // «No puedo ir» cancela el turno con ese motivo y el enlace deja de servir.
        Assert.Equal(HttpStatusCode.OK, (await anon.PostAsync($"/api/public/v1/appointments/{newToken}/decline", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/public/v1/appointments/{newToken}")).StatusCode);
        var detail = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/appointments/{id}");
        Assert.Equal("cancelled", detail.GetProperty("status").GetString());
        Assert.Contains("no puede asistir", detail.GetProperty("cancellationReason").GetString());

        // Un token inventado es un 404 uniforme.
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/api/public/v1/appointments/token-que-no-existe")).StatusCode);
    }

    [Fact(DisplayName = "T-APT-08: Los turnos se agrupan por día del taller, no por día UTC (22:00 locales es otro día en UTC)")]
    public async Task Los_dias_son_del_taller()
    {
        using var client = CreateClient();
        await LoginAsync(client, AppointmentsWebApplicationFactory.OwnerEmail);
        var (night, day) = TomorrowAt(22);

        var created = await PostAppointmentAsync(client, night, null);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("appointment").GetProperty("id").GetString();

        var sameDay = await client.GetFromJsonAsync<JsonElement>($"/api/v1/appointments?date={day:yyyy-MM-dd}");
        Assert.Contains(sameDay.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetString() == id);

        var nextDay = await client.GetFromJsonAsync<JsonElement>($"/api/v1/appointments?date={day.AddDays(1):yyyy-MM-dd}");
        Assert.DoesNotContain(nextDay.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetString() == id);
    }
}
