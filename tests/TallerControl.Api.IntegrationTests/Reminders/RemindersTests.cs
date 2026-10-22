using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TallerControl.Api.Identity;
using TallerControl.Api.IntegrationTests.Persistence;

namespace TallerControl.Api.IntegrationTests.Reminders;

/// <summary>
/// E13 bloque 3: tests de integración de recordatorios automáticos.
/// T-REM-01: CRUD básico de recordatorio.
/// T-REM-02: EvaluateDue crea notificaciones para recordatorios vencidos.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RemindersTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private RemindersWebApplicationFactory _factory = null!;

    public RemindersTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        _factory = await RemindersWebApplicationFactory.CreateAsync(_postgres);
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

    private async Task<(string id, uint version)> CreateReminderAsync(
        HttpClient client, Guid vehicleId,
        string kind = "service_due",
        string title = "Service",
        string? dueDate = null,
        int? dueKm = null)
    {
        var csrf = await FetchCsrfAsync(client);

        object body;
        if (dueDate is not null)
            body = new { vehicleId, kind, title, dueDate = DateTimeOffset.Parse(dueDate) };
        else if (dueKm is not null)
            body = new { vehicleId, kind, title, dueKm };
        else
            body = new { vehicleId, kind, title, dueDate = DateTimeOffset.UtcNow.AddDays(30) };

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/reminders")
        {
            Content = JsonContent.Create(body),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return (json.GetProperty("id").GetString()!, json.GetProperty("version").GetUInt32());
    }

    [Fact(DisplayName = "T-REM-01: CRUD completo de recordatorio")]
    public async Task Crud_de_recordatorio()
    {
        using var client = CreateClient();
        await LoginAsync(client, RemindersWebApplicationFactory.OwnerEmail, RemindersWebApplicationFactory.OwnerPassword);

        // POST: crear
        var (id, version) = await CreateReminderAsync(client, _factory.VehicleId,
            title: "Cambio de aceite", dueDate: DateTimeOffset.UtcNow.AddDays(14).ToString("O"));

        // GET: leer
        var getResp = await client.GetAsync($"/api/v1/reminders/{id}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var dto = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Cambio de aceite", dto.GetProperty("title").GetString());
        Assert.Equal("service_due", dto.GetProperty("kind").GetString());

        // GET lista con filtro de vehículo
        var listResp = await client.GetAsync($"/api/v1/reminders?vehicleId={_factory.VehicleId}");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var list = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(list.GetProperty("total").GetInt32() >= 1);

        // PUT: actualizar con If-Match
        var csrf = await FetchCsrfAsync(client);
        var updateReq = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/reminders/{id}")
        {
            Content = JsonContent.Create(new
            {
                vehicleId = _factory.VehicleId,
                kind = "inspection_due",
                title = "VTV vencimiento",
                dueDate = DateTimeOffset.UtcNow.AddDays(30),
            }),
        };
        updateReq.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        updateReq.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        var updateResp = await client.SendAsync(updateReq);
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        var updated = await updateResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("inspection_due", updated.GetProperty("kind").GetString());
        Assert.Equal("VTV vencimiento", updated.GetProperty("title").GetString());

        // DELETE: borrar
        csrf = await FetchCsrfAsync(client);
        var delReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/reminders/{id}");
        delReq.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        var delResp = await client.SendAsync(delReq);
        Assert.Equal(HttpStatusCode.NoContent, delResp.StatusCode);

        // GET tras borrar: 404
        var notFoundResp = await client.GetAsync($"/api/v1/reminders/{id}");
        Assert.Equal(HttpStatusCode.NotFound, notFoundResp.StatusCode);
    }

    [Fact(DisplayName = "T-REM-01b: recordatorio sin dueDate ni dueKm devuelve 422")]
    public async Task Recordatorio_sin_fecha_ni_km_es_invalido()
    {
        using var client = CreateClient();
        await LoginAsync(client, RemindersWebApplicationFactory.OwnerEmail, RemindersWebApplicationFactory.OwnerPassword);

        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/reminders")
        {
            Content = JsonContent.Create(new
            {
                vehicleId = _factory.VehicleId,
                kind = "service_due",
                title = "Sin fecha ni km",
            }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    [Fact(DisplayName = "T-REM-02: POST /reminders/evaluate crea notificaciones para recordatorios vencidos")]
    public async Task Evaluate_crea_notificacion_para_recordatorio_vencido()
    {
        using var client = CreateClient();
        await LoginAsync(client, RemindersWebApplicationFactory.OwnerEmail, RemindersWebApplicationFactory.OwnerPassword);

        // Crear recordatorio vencido (dueDate en el pasado)
        await CreateReminderAsync(client, _factory.VehicleId,
            title: "Recordatorio vencido test",
            dueDate: DateTimeOffset.UtcNow.AddDays(-1).ToString("O"));

        // Ejecutar evaluate
        var csrf = await FetchCsrfAsync(client);
        var evalReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/reminders/evaluate")
        {
            Content = JsonContent.Create(new { }),
        };
        evalReq.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        var evalResp = await client.SendAsync(evalReq);

        Assert.Equal(HttpStatusCode.OK, evalResp.StatusCode);
        var result = await evalResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.GetProperty("notificationsCreated").GetInt32() >= 1);
    }

    [Fact(DisplayName = "T-REM-03: La tarea periódica avisa los recordatorios vencidos sin que nadie toque «Evaluar»")]
    public async Task Tarea_periodica_evalua_los_recordatorios_de_todos_los_talleres()
    {
        using var client = CreateClient();
        await LoginAsync(client, RemindersWebApplicationFactory.OwnerEmail, RemindersWebApplicationFactory.OwnerPassword);
        var (id, _) = await CreateReminderAsync(client, _factory.VehicleId,
            title: "Vence solo",
            dueDate: DateTimeOffset.UtcNow.AddDays(-2).ToString("O"));

        var worker = _factory.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .OfType<TallerControl.Infrastructure.Maintenance.MaintenanceWorker>().Single();
        await worker.RunOnceAsync(CancellationToken.None);

        var reminder = await client.GetFromJsonAsync<JsonElement>($"/api/v1/reminders/{id}");
        Assert.NotEqual(JsonValueKind.Null, reminder.GetProperty("lastNotifiedAt").ValueKind);
    }
}
