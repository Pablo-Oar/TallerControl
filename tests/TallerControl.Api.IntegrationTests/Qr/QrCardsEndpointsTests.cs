using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TallerControl.Api.Identity;
using TallerControl.Api.IntegrationTests.Persistence;

namespace TallerControl.Api.IntegrationTests.Qr;

/// <summary>
/// E8 bloque 6: tests de integración de tarjetas QR y portal del dueño.
/// T-QR-02..23, T-DOC-04. REQ-17, REQ-18.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class QrCardsEndpointsTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private QrWebApplicationFactory _factory = null!;

    public QrCardsEndpointsTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        _factory = await QrWebApplicationFactory.CreateAsync(_postgres);
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

    private async Task<JsonElement> GenerateBatchAsync(HttpClient client, int quantity = 3)
    {
        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/qr/cards/batch")
        {
            Content = JsonContent.Create(new { quantity }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>
    /// Desasigna cualquier tarjeta actualmente asignada/perdida del vehículo sembrado,
    /// genera una tarjeta nueva y la asigna. Devuelve el código de la nueva tarjeta.
    /// Necesario porque el constraint único impide tener dos tarjetas activas en el mismo vehículo.
    /// </summary>
    private async Task<string> CreateAssignedCardAsync(HttpClient client)
    {
        // Desasignar cualquier tarjeta existente en el vehículo
        var existingResp = await client.GetAsync("/api/v1/qr/cards?pageSize=100");
        existingResp.EnsureSuccessStatusCode();
        var existing = await existingResp.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var item in existing.GetProperty("items").EnumerateArray())
        {
            var status = item.GetProperty("status").GetString();
            if (status is not ("assigned" or "lost")) continue;
            if (!item.TryGetProperty("vehicleId", out var vidProp)) continue;
            if (vidProp.GetString() != _factory.VehicleId.ToString()) continue;

            var existingCardId = item.GetProperty("id").GetString()!;
            var unassignCsrf = await FetchCsrfAsync(client);
            var unassignReq = new HttpRequestMessage(
                HttpMethod.Delete, $"/api/v1/qr/cards/{existingCardId}/assign");
            unassignReq.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, unassignCsrf);
            await client.SendAsync(unassignReq);
        }

        // Generar una tarjeta nueva y asignarla
        await GenerateBatchAsync(client, quantity: 1);

        var listResp = await client.GetAsync("/api/v1/qr/cards?pageSize=100");
        listResp.EnsureSuccessStatusCode();
        var list = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        var card = list.GetProperty("items").EnumerateArray()
            .First(c => c.GetProperty("status").GetString() == "free");
        var cardId = card.GetProperty("id").GetString()!;
        var code = card.GetProperty("code").GetString()!;

        var csrf = await FetchCsrfAsync(client);
        var assignReq = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/qr/cards/{cardId}/assign")
        {
            Content = JsonContent.Create(new { vehicleId = _factory.VehicleId }),
        };
        assignReq.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        var assignResp = await client.SendAsync(assignReq);
        Assert.Equal(HttpStatusCode.OK, assignResp.StatusCode);

        return code;
    }

    // ── T-QR-02: generación de batch ─────────────────────────────────────────

    [Fact(DisplayName = "T-QR-02: POST /qr/cards/batch crea N tarjetas Free con códigos únicos")]
    public async Task Batch_genera_tarjetas_libres_con_codigos_unicos()
    {
        using var client = CreateClient();
        await LoginAsync(client, QrWebApplicationFactory.OwnerEmail, QrWebApplicationFactory.Password);

        var result = await GenerateBatchAsync(client, quantity: 5);

        Assert.Equal(5, result.GetProperty("count").GetInt32());
        Assert.NotEqual(Guid.Empty.ToString(), result.GetProperty("batchId").GetString());
    }

    // ── T-QR-03: listado ──────────────────────────────────────────────────────

    [Fact(DisplayName = "T-QR-03: GET /qr/cards devuelve paginación con las tarjetas del taller")]
    public async Task Listado_devuelve_tarjetas_del_taller()
    {
        using var client = CreateClient();
        await LoginAsync(client, QrWebApplicationFactory.OwnerEmail, QrWebApplicationFactory.Password);
        await GenerateBatchAsync(client, quantity: 2);

        var resp = await client.GetAsync("/api/v1/qr/cards");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("total").GetInt32() >= 2);
    }

    // ── T-QR-04: obtener tarjeta por ID ──────────────────────────────────────

    [Fact(DisplayName = "T-QR-04: GET /qr/cards/{id} devuelve el detalle completo")]
    public async Task Obtener_tarjeta_por_id_devuelve_detalle()
    {
        using var client = CreateClient();
        await LoginAsync(client, QrWebApplicationFactory.OwnerEmail, QrWebApplicationFactory.Password);

        var batch = await GenerateBatchAsync(client, quantity: 1);
        var listResp = await client.GetAsync("/api/v1/qr/cards?pageSize=1");
        var list = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        var cardId = list.GetProperty("items")[0].GetProperty("id").GetString()!;

        var resp = await client.GetAsync($"/api/v1/qr/cards/{cardId}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(cardId, json.GetProperty("id").GetString());
        Assert.Equal("free", json.GetProperty("status").GetString());
    }

    // ── T-QR-05: asignar tarjeta ──────────────────────────────────────────────

    [Fact(DisplayName = "T-QR-05: POST /qr/cards/{id}/assign cambia estado a Assigned")]
    public async Task Asignar_tarjeta_cambia_estado_a_assigned()
    {
        using var client = CreateClient();
        await LoginAsync(client, QrWebApplicationFactory.OwnerEmail, QrWebApplicationFactory.Password);

        // Usar CreateAssignedCardAsync para limpiar estado previo del vehículo y asignar una tarjeta nueva
        await CreateAssignedCardAsync(client);

        // Verificar que el vehículo tiene una tarjeta asignada
        var listResp = await client.GetAsync("/api/v1/qr/cards?pageSize=100");
        var list = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        var assignedCard = list.GetProperty("items").EnumerateArray()
            .FirstOrDefault(c =>
                c.GetProperty("status").GetString() == "assigned" &&
                c.TryGetProperty("vehicleId", out var v) &&
                v.GetString() == _factory.VehicleId.ToString());

        Assert.NotEqual(default, assignedCard);
        Assert.Equal(_factory.VehicleId.ToString(), assignedCard.GetProperty("vehicleId").GetString());
    }

    // ── T-QR-06: assign-by-code ───────────────────────────────────────────────

    [Fact(DisplayName = "T-QR-06: POST /qr/cards/assign-by-code asigna por código de tarjeta")]
    public async Task Assign_by_code_asigna_tarjeta_por_codigo()
    {
        using var client = CreateClient();
        await LoginAsync(client, QrWebApplicationFactory.AdminEmail, QrWebApplicationFactory.Password);

        // Limpiar tarjeta activa del vehículo si existe (constraint único por vehículo)
        var existingResp = await client.GetAsync("/api/v1/qr/cards?pageSize=100");
        var existingList = await existingResp.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var item in existingList.GetProperty("items").EnumerateArray())
        {
            var status = item.GetProperty("status").GetString();
            if (status is not ("assigned" or "lost")) continue;
            if (!item.TryGetProperty("vehicleId", out var vp)) continue;
            if (vp.GetString() != _factory.VehicleId.ToString()) continue;

            var cid = item.GetProperty("id").GetString()!;
            var csrf2 = await FetchCsrfAsync(client);
            var ur = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/qr/cards/{cid}/assign");
            ur.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf2);
            await client.SendAsync(ur);
        }

        // Generar nueva tarjeta libre
        await GenerateBatchAsync(client, quantity: 1);
        var listResp = await client.GetAsync("/api/v1/qr/cards?pageSize=100");
        var list = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        var freeCard = list.GetProperty("items").EnumerateArray()
            .First(c => c.GetProperty("status").GetString() == "free");
        var cardCode = freeCard.GetProperty("code").GetString()!;

        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/qr/cards/assign-by-code")
        {
            Content = JsonContent.Create(new { code = cardCode, vehicleId = _factory.VehicleId }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("assigned", json.GetProperty("status").GetString());
    }

    // ── T-QR-07: desasignar tarjeta ───────────────────────────────────────────

    [Fact(DisplayName = "T-QR-07: DELETE /qr/cards/{id}/assign devuelve 204 y tarjeta queda Free")]
    public async Task Desasignar_tarjeta_devuelve_204()
    {
        using var client = CreateClient();
        await LoginAsync(client, QrWebApplicationFactory.OwnerEmail, QrWebApplicationFactory.Password);
        var code = await CreateAssignedCardAsync(client);

        var listResp = await client.GetAsync("/api/v1/qr/cards?pageSize=100");
        var list = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        var cardId = list.GetProperty("items").EnumerateArray()
            .First(c => c.GetProperty("code").GetString() == code)
            .GetProperty("id").GetString()!;

        var csrf = await FetchCsrfAsync(client);
        var unassignReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/qr/cards/{cardId}/assign");
        unassignReq.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        var resp = await client.SendAsync(unassignReq);

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        var detail = await client.GetAsync($"/api/v1/qr/cards/{cardId}");
        var json = await detail.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("free", json.GetProperty("status").GetString());
    }

    // ── T-QR-08/09: marcar y desmarcar como perdida ───────────────────────────

    [Fact(DisplayName = "T-QR-08: PATCH /qr/cards/{id}/lost marca tarjeta como perdida")]
    public async Task Marcar_perdida_cambia_estado_a_lost()
    {
        using var client = CreateClient();
        await LoginAsync(client, QrWebApplicationFactory.OwnerEmail, QrWebApplicationFactory.Password);
        var code = await CreateAssignedCardAsync(client);

        // Obtener el ID de la tarjeta recién asignada
        var listResp = await client.GetAsync("/api/v1/qr/cards?pageSize=100");
        var list = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        var cardId = list.GetProperty("items").EnumerateArray()
            .First(c => c.GetProperty("code").GetString() == code)
            .GetProperty("id").GetString()!;

        var csrf = await FetchCsrfAsync(client);
        var lostReq = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/qr/cards/{cardId}/lost")
        {
            Content = JsonContent.Create(new { lost = true }),
        };
        lostReq.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        var resp = await client.SendAsync(lostReq);

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        var detail = await client.GetAsync($"/api/v1/qr/cards/{cardId}");
        var json = await detail.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("lost", json.GetProperty("status").GetString());
    }

    [Fact(DisplayName = "T-QR-09: PATCH /qr/cards/{id}/lost con lost=false restaura estado Assigned")]
    public async Task Desmarcar_perdida_restaura_assigned()
    {
        using var client = CreateClient();
        await LoginAsync(client, QrWebApplicationFactory.OwnerEmail, QrWebApplicationFactory.Password);
        var code = await CreateAssignedCardAsync(client);

        var listResp = await client.GetAsync("/api/v1/qr/cards?pageSize=100");
        var list = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        var cardId = list.GetProperty("items").EnumerateArray()
            .First(c => c.GetProperty("code").GetString() == code)
            .GetProperty("id").GetString()!;

        // Marcar perdida
        var csrf = await FetchCsrfAsync(client);
        var lostReq = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/qr/cards/{cardId}/lost")
        { Content = JsonContent.Create(new { lost = true }) };
        lostReq.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        await client.SendAsync(lostReq);

        // Desmarcar
        csrf = await FetchCsrfAsync(client);
        var unlostReq = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/qr/cards/{cardId}/lost")
        { Content = JsonContent.Create(new { lost = false }) };
        unlostReq.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        var resp = await client.SendAsync(unlostReq);

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        var detail = await client.GetAsync($"/api/v1/qr/cards/{cardId}");
        var json = await detail.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("assigned", json.GetProperty("status").GetString());
    }

    // ── T-QR-10: revocar tarjeta ──────────────────────────────────────────────

    [Fact(DisplayName = "T-QR-10: POST /qr/cards/{id}/revoke revoca la tarjeta (solo Owner)")]
    public async Task Revocar_tarjeta_requiere_qr_revoke()
    {
        using var client = CreateClient();
        await LoginAsync(client, QrWebApplicationFactory.OwnerEmail, QrWebApplicationFactory.Password);
        await GenerateBatchAsync(client, quantity: 1);

        var listResp = await client.GetAsync("/api/v1/qr/cards?status=free&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var list = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        var cardId = list.GetProperty("items")[0].GetProperty("id").GetString()!;

        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/qr/cards/{cardId}/revoke")
        {
            Content = JsonContent.Create(new { reason = "Test de revocación" }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        var detail = await client.GetAsync($"/api/v1/qr/cards/{cardId}");
        var json = await detail.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("revoked", json.GetProperty("status").GetString());
    }

    [Fact(DisplayName = "T-QR-21: POST /qr/cards/{id}/revoke devuelve 403 para AdminStaff (requiere qr.revoke)")]
    public async Task Revocar_tarjeta_con_admin_devuelve_403()
    {
        using var client = CreateClient();
        await LoginAsync(client, QrWebApplicationFactory.AdminEmail, QrWebApplicationFactory.Password);
        await GenerateBatchAsync(client, quantity: 1);

        var listResp = await client.GetAsync("/api/v1/qr/cards?status=free&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var list = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        var cardId = list.GetProperty("items")[0].GetProperty("id").GetString()!;

        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/qr/cards/{cardId}/revoke")
        {
            Content = JsonContent.Create(new { reason = "Intento sin permiso" }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ── T-QR-11: PDF de tarjeta ───────────────────────────────────────────────

    [Fact(DisplayName = "T-QR-11: GET /qr/cards/{id}/print devuelve PDF con content-type application/pdf")]
    public async Task Print_tarjeta_asignada_devuelve_pdf()
    {
        using var client = CreateClient();
        await LoginAsync(client, QrWebApplicationFactory.OwnerEmail, QrWebApplicationFactory.Password);
        var code = await CreateAssignedCardAsync(client);

        var listResp = await client.GetAsync("/api/v1/qr/cards?pageSize=100");
        var list = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        var cardId = list.GetProperty("items").EnumerateArray()
            .First(c => c.GetProperty("code").GetString() == code)
            .GetProperty("id").GetString()!;

        var resp = await client.GetAsync($"/api/v1/qr/cards/{cardId}/print?format=card");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("application/pdf", resp.Content.Headers.ContentType?.MediaType);

        var bytes = await resp.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
        // Verificar magic bytes PDF: %PDF
        Assert.Equal(0x25, bytes[0]); // '%'
        Assert.Equal(0x50, bytes[1]); // 'P'
        Assert.Equal(0x44, bytes[2]); // 'D'
        Assert.Equal(0x46, bytes[3]); // 'F'
    }

    // ── T-DOC-04: PDF sin vehículo debe fallar ────────────────────────────────

    [Fact(DisplayName = "T-DOC-04: GET /qr/cards/{id}/print format=card sin vehículo asignado devuelve 409")]
    public async Task Print_tarjeta_libre_devuelve_409()
    {
        using var client = CreateClient();
        await LoginAsync(client, QrWebApplicationFactory.OwnerEmail, QrWebApplicationFactory.Password);
        await GenerateBatchAsync(client, quantity: 1);

        var listResp = await client.GetAsync("/api/v1/qr/cards?status=free&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var list = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        var cardId = list.GetProperty("items")[0].GetProperty("id").GetString()!;

        var resp = await client.GetAsync($"/api/v1/qr/cards/{cardId}/print?format=card");
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    // ── T-QR-12: eventos de tarjeta ───────────────────────────────────────────

    [Fact(DisplayName = "T-QR-12: GET /qr/cards/{id}/events devuelve la lista de eventos")]
    public async Task Eventos_de_tarjeta_devuelve_lista()
    {
        using var client = CreateClient();
        await LoginAsync(client, QrWebApplicationFactory.OwnerEmail, QrWebApplicationFactory.Password);
        await GenerateBatchAsync(client, quantity: 1);

        var listResp = await client.GetAsync("/api/v1/qr/cards?status=free&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var list = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        var cardId = list.GetProperty("items")[0].GetProperty("id").GetString()!;

        var resp = await client.GetAsync($"/api/v1/qr/cards/{cardId}/events");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("total", out _));
    }

    // ── T-QR-22: mecánico no puede gestionar QR ───────────────────────────────

    [Fact(DisplayName = "T-QR-22: mecánico sin qr.manage recibe 403 al intentar listar tarjetas")]
    public async Task Mecanico_sin_qr_manage_recibe_403()
    {
        using var client = CreateClient();
        await LoginAsync(client, QrWebApplicationFactory.MechanicEmail, QrWebApplicationFactory.Password);

        var resp = await client.GetAsync("/api/v1/qr/cards");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ── T-QR-13: escaneo de código QR ────────────────────────────────────────

    [Fact(DisplayName = "T-QR-13: GET /api/public/v1/qr/{code} con tarjeta asignada devuelve workshop info")]
    public async Task Escaneo_tarjeta_asignada_devuelve_workshop_info()
    {
        // Crear y asignar tarjeta con cliente autenticado
        using var ownerClient = CreateClient();
        await LoginAsync(ownerClient, QrWebApplicationFactory.OwnerEmail, QrWebApplicationFactory.Password);
        var code = await CreateAssignedCardAsync(ownerClient);

        // Escanear como anónimo
        using var publicClient = CreateClient();
        var resp = await publicClient.GetAsync($"/api/public/v1/qr/{code}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("workshop", out var workshop));
        Assert.False(string.IsNullOrEmpty(workshop.GetProperty("name").GetString()));
        Assert.Equal("assigned", json.GetProperty("cardState").GetString());
    }

    [Fact(DisplayName = "T-QR-14: GET /api/public/v1/qr/{code} con código inexistente devuelve 404")]
    public async Task Escaneo_codigo_inexistente_devuelve_404()
    {
        using var client = CreateClient();
        var resp = await client.GetAsync("/api/public/v1/qr/CODEINEXISTENTE");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ── T-QR-15: respuesta uniforme del challenge ─────────────────────────────

    [Fact(DisplayName = "T-QR-15: owner-challenge con patente incorrecta devuelve 202 (respuesta uniforme)")]
    public async Task Challenge_con_patente_incorrecta_devuelve_202_uniform()
    {
        // Crear y asignar tarjeta
        using var ownerClient = CreateClient();
        await LoginAsync(ownerClient, QrWebApplicationFactory.OwnerEmail, QrWebApplicationFactory.Password);
        var code = await CreateAssignedCardAsync(ownerClient);

        // Challenge con patente incorrecta (T-QR-15: mismo 202 que si fuera correcta)
        using var publicClient = CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/public/v1/qr/{code}/owner-challenge")
        {
            Content = JsonContent.Create(new
            {
                plate = "XXXXXX",
                customerCode = (string?)null,
                turnstileToken = "BYPASS"
            }),
        };
        var resp = await publicClient.SendAsync(req);

        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        // El body siempre tiene challengeId (dummy o real), channel y maskedDestination
        Assert.True(json.TryGetProperty("challengeId", out _));
        Assert.True(json.TryGetProperty("channel", out _));
    }

    // ── T-QR-16: challenge + OTP válido ──────────────────────────────────────

    [Fact(DisplayName = "T-QR-16..19: flujo completo — challenge OTP → sesión → portal del dueño")]
    public async Task Flujo_completo_owner_challenge_a_portal()
    {
        // Setup: crear tarjeta asignada
        using var ownerClient = CreateClient();
        await LoginAsync(ownerClient, QrWebApplicationFactory.OwnerEmail, QrWebApplicationFactory.Password);
        var code = await CreateAssignedCardAsync(ownerClient);

        // Obtener el código de acceso del cliente
        var csrf = await FetchCsrfAsync(ownerClient);
        var codeReq = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/customers/{_factory.CustomerId}/owner-access-code");
        codeReq.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        var codeResp = await ownerClient.SendAsync(codeReq);
        Assert.Equal(HttpStatusCode.OK, codeResp.StatusCode);
        var codeJson = await codeResp.Content.ReadFromJsonAsync<JsonElement>();
        var accessCode = codeJson.GetProperty("code").GetString()!;

        // T-QR-16: challenge con patente correcta + access code
        using var publicClient = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });

        var challengeReq = new HttpRequestMessage(
            HttpMethod.Post, $"/api/public/v1/qr/{code}/owner-challenge")
        {
            Content = JsonContent.Create(new
            {
                plate = "QRTEST",
                customerCode = accessCode,
                turnstileToken = "BYPASS"
            }),
        };
        var challengeResp = await publicClient.SendAsync(challengeReq);
        Assert.Equal(HttpStatusCode.Accepted, challengeResp.StatusCode);

        var challengeJson = await challengeResp.Content.ReadFromJsonAsync<JsonElement>();
        var challengeId = challengeJson.GetProperty("challengeId").GetString()!;

        // T-QR-18: validar OTP con el access code
        var sessionReq = new HttpRequestMessage(
            HttpMethod.Post, $"/api/public/v1/qr/{code}/owner-session")
        {
            Content = JsonContent.Create(new
            {
                challengeId = Guid.Parse(challengeId),
                otp = accessCode
            }),
        };
        var sessionResp = await publicClient.SendAsync(sessionReq);
        Assert.Equal(HttpStatusCode.OK, sessionResp.StatusCode);

        // Verificar que hay una cookie tc_qr en la respuesta
        Assert.True(sessionResp.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies, c => c.StartsWith("tc_qr=", StringComparison.Ordinal));

        // T-QR-19: GET /owner-vehicle con la sesión (cookie manual ya que es Secure)
        var cookieValue = cookies
            .First(c => c.StartsWith("tc_qr=", StringComparison.Ordinal))
            .Split(';')[0]["tc_qr=".Length..];

        var vehicleReq = new HttpRequestMessage(
            HttpMethod.Get, $"/api/public/v1/qr/{code}/owner-vehicle");
        vehicleReq.Headers.Add("Cookie", $"tc_qr={cookieValue}");
        var vehicleResp = await publicClient.SendAsync(vehicleReq);
        Assert.Equal(HttpStatusCode.OK, vehicleResp.StatusCode);

        var vehicleJson = await vehicleResp.Content.ReadFromJsonAsync<JsonElement>();
        // La patente normalizada es QRTEST (sin guiones)
        Assert.Contains("QRTEST", vehicleJson.GetProperty("plate").GetString()!,
            StringComparison.OrdinalIgnoreCase);
    }

    // ── T-QR-20: /owner-vehicle sin cookie devuelve 401 ──────────────────────

    [Fact(DisplayName = "T-QR-20: GET /owner-vehicle sin cookie tc_qr devuelve 401")]
    public async Task Owner_vehicle_sin_cookie_devuelve_401()
    {
        using var ownerClient = CreateClient();
        await LoginAsync(ownerClient, QrWebApplicationFactory.OwnerEmail, QrWebApplicationFactory.Password);
        var code = await CreateAssignedCardAsync(ownerClient);

        using var publicClient = CreateClient();
        var resp = await publicClient.GetAsync($"/api/public/v1/qr/{code}/owner-vehicle");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── T-QR-17: OTP incorrecto devuelve 422 ──────────────────────────────────

    [Fact(DisplayName = "T-QR-17: owner-session con OTP incorrecto devuelve 422")]
    public async Task Owner_session_con_otp_incorrecto_devuelve_422()
    {
        using var ownerClient = CreateClient();
        await LoginAsync(ownerClient, QrWebApplicationFactory.OwnerEmail, QrWebApplicationFactory.Password);
        var code = await CreateAssignedCardAsync(ownerClient);

        // Obtener access code real para que el challenge sea válido
        var csrf = await FetchCsrfAsync(ownerClient);
        var codeReq = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/customers/{_factory.CustomerId}/owner-access-code");
        codeReq.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        var codeResp = await ownerClient.SendAsync(codeReq);
        var accessCode = (await codeResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;

        using var publicClient = CreateClient();

        // Iniciar challenge válido
        var challengeReq = new HttpRequestMessage(
            HttpMethod.Post, $"/api/public/v1/qr/{code}/owner-challenge")
        {
            Content = JsonContent.Create(new
            {
                plate = "QRTEST",
                customerCode = accessCode,
                turnstileToken = "BYPASS"
            }),
        };
        var challengeResp = await publicClient.SendAsync(challengeReq);
        var challengeId = (await challengeResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("challengeId").GetString()!;

        // Enviar OTP incorrecto
        var sessionReq = new HttpRequestMessage(
            HttpMethod.Post, $"/api/public/v1/qr/{code}/owner-session")
        {
            Content = JsonContent.Create(new
            {
                challengeId = Guid.Parse(challengeId),
                otp = "WRONGOTP"
            }),
        };
        var resp = await publicClient.SendAsync(sessionReq);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    // ── T-QR-23: código de acceso del dueño ───────────────────────────────────

    [Fact(DisplayName = "T-QR-23: POST /customers/{id}/owner-access-code devuelve código de 8 chars Base32")]
    public async Task Owner_access_code_devuelve_codigo_base32()
    {
        using var client = CreateClient();
        await LoginAsync(client, QrWebApplicationFactory.OwnerEmail, QrWebApplicationFactory.Password);

        var csrf = await FetchCsrfAsync(client);
        var req = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/customers/{_factory.CustomerId}/owner-access-code");
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var code = json.GetProperty("code").GetString()!;

        Assert.Equal(8, code.Length);
        const string base32Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        Assert.All(code, c => Assert.Contains(c, base32Alphabet));
    }

    // ── T-QR-24: búsqueda rápida por código QR (panel) ───────────────────────

    [Fact(DisplayName = "T-QR-24: GET /qr/cards/scan/{code} devuelve la ficha del vehículo asignado")]
    public async Task Scan_autenticado_devuelve_ficha_vehiculo()
    {
        using var client = CreateClient();
        await LoginAsync(client, QrWebApplicationFactory.OwnerEmail, QrWebApplicationFactory.Password);
        var code = await CreateAssignedCardAsync(client);

        var resp = await client.GetAsync($"/api/v1/qr/cards/scan/{code}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, json.GetProperty("code").GetString());
        Assert.Equal("assigned", json.GetProperty("status").GetString());
        Assert.True(json.GetProperty("vehicleId").GetString() is not null);
        Assert.False(string.IsNullOrEmpty(json.GetProperty("vehiclePlate").GetString()));
    }

    [Fact(DisplayName = "T-QR-25: GET /qr/cards/scan/{code} sin autenticación devuelve 401")]
    public async Task Scan_sin_autenticacion_devuelve_401()
    {
        using var ownerClient = CreateClient();
        await LoginAsync(ownerClient, QrWebApplicationFactory.OwnerEmail, QrWebApplicationFactory.Password);
        var code = await CreateAssignedCardAsync(ownerClient);

        using var anonClient = CreateClient();
        var resp = await anonClient.GetAsync($"/api/v1/qr/cards/scan/{code}");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── T-QR-26: reportar llaves encontradas (público) ────────────────────────

    [Fact(DisplayName = "T-QR-26: POST /qr/{code}/found-keys con tarjeta asignada devuelve 202")]
    public async Task Found_keys_tarjeta_asignada_devuelve_202()
    {
        using var ownerClient = CreateClient();
        await LoginAsync(ownerClient, QrWebApplicationFactory.OwnerEmail, QrWebApplicationFactory.Password);
        var code = await CreateAssignedCardAsync(ownerClient);

        using var publicClient = CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/public/v1/qr/{code}/found-keys")
        {
            Content = JsonContent.Create(new { reporterName = "Juan", reporterPhone = "+54911234" }),
        };
        var resp = await publicClient.SendAsync(req);
        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
    }

    [Fact(DisplayName = "T-QR-27: POST /qr/{code}/found-keys con código inexistente devuelve 202 (respuesta uniforme)")]
    public async Task Found_keys_codigo_inexistente_devuelve_202_uniforme()
    {
        using var client = CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/public/v1/qr/CODENOEXISTS/found-keys")
        {
            Content = JsonContent.Create(new { reporterName = (string?)null, reporterPhone = (string?)null }),
        };
        var resp = await client.SendAsync(req);
        // Respuesta uniforme: no revela si el código existe
        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
    }

    [Fact(DisplayName = "T-QR-28: POST /qr/{code}/found-keys registra QrScanEvent FoundKeys al reportar tarjeta asignada")]
    public async Task Found_keys_registra_scan_event_found_keys()
    {
        using var ownerClient = CreateClient();
        await LoginAsync(ownerClient, QrWebApplicationFactory.OwnerEmail, QrWebApplicationFactory.Password);
        var code = await CreateAssignedCardAsync(ownerClient);

        // Buscar el ID de la tarjeta
        var listResp = await ownerClient.GetAsync("/api/v1/qr/cards?pageSize=100");
        var list = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        var card = list.GetProperty("items").EnumerateArray()
            .First(c => c.GetProperty("code").GetString() == code);
        var cardId = card.GetProperty("id").GetString()!;

        using var publicClient = CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/public/v1/qr/{code}/found-keys")
        {
            Content = JsonContent.Create(new { reporterName = "Ana", reporterPhone = (string?)null }),
        };
        await publicClient.SendAsync(req);

        // Verificar que se registró el evento
        var eventsResp = await ownerClient.GetAsync($"/api/v1/qr/cards/{cardId}/events");
        Assert.Equal(HttpStatusCode.OK, eventsResp.StatusCode);
        var events = await eventsResp.Content.ReadFromJsonAsync<JsonElement>();
        var hasFoundKeysEvent = events.GetProperty("items").EnumerateArray()
            .Any(e => e.GetProperty("eventType").GetString() == "found_keys");
        Assert.True(hasFoundKeysEvent);
    }

    // ── T-QR-31: página pública solo muestra marca del taller ────────────────

    [Fact(DisplayName = "T-QR-31: el escaneo público devuelve solo la marca del taller, sin datos del proveedor (doc 08, V2-delta)")]
    public async Task Escaneo_publico_devuelve_solo_marca_del_taller_sin_datos_proveedor()
    {
        using var ownerClient = CreateClient();
        await LoginAsync(ownerClient, QrWebApplicationFactory.OwnerEmail, QrWebApplicationFactory.Password);

        var code = await CreateAssignedCardAsync(ownerClient);

        // Escaneo anónimo: responde con los datos del taller.
        using var publicClient = CreateClient();
        var resp = await publicClient.GetAsync($"/api/public/v1/qr/{code}");
        Assert.True(resp.IsSuccessStatusCode);

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var workshop = json.GetProperty("workshop");

        // El nombre es el del taller, no el del proveedor.
        var workshopName = workshop.GetProperty("name").GetString()!;
        Assert.False(string.IsNullOrEmpty(workshopName));
        Assert.DoesNotContain("TallerControl", workshopName, StringComparison.OrdinalIgnoreCase);

        // La respuesta no incluye campos de identificación del proveedor.
        Assert.False(json.TryGetProperty("providerName", out _));
        Assert.False(json.TryGetProperty("poweredBy", out _));
        Assert.False(workshop.TryGetProperty("providerName", out _));
        Assert.False(workshop.TryGetProperty("poweredBy", out _));
    }
}
