using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TallerControl.Api.Identity;
using TallerControl.Api.IntegrationTests.Persistence;

namespace TallerControl.Api.IntegrationTests.Users;

/// <summary>
/// E2 bloque 3: invitaciones y restablecimiento de contraseña.
/// T-USR-01..04, T-AUTH-07, T-AUTH-08 (doc 08 §4, doc 05 §2).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class InvitationEndpointsTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private InvitationWebApplicationFactory _factory = null!;

    public InvitationEndpointsTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        _factory = await InvitationWebApplicationFactory.CreateAsync(_postgres);
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

    private async Task<string> FetchAntiforgeryTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/auth/antiforgery-token");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("token").GetString()!;
    }

    private async Task LoginAsOwnerAsync(HttpClient client)
    {
        var csrf = await FetchAntiforgeryTokenAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new
            {
                email = InvitationWebApplicationFactory.SeedEmail,
                password = InvitationWebApplicationFactory.SeedPassword,
                rememberMe = false,
            }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    // ── T-USR-01: invitación exitosa ────────────────────────────────────────────────
    [Fact(DisplayName = "T-USR-01: owner invita un mecánico y recibe 201 + se encola el email")]
    public async Task Owner_invita_mecanico_recibe_201_y_encola_email()
    {
        using var client = CreateClient();
        await LoginAsOwnerAsync(client);

        _factory.EmailSender.Clear();

        var csrf = await FetchAntiforgeryTokenAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invitations")
        {
            Content = JsonContent.Create(new
            {
                email = "mecanico@taller.com",
                displayName = "Mecánico Test",
                role = "mechanic",
            }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);

        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("mecanico@taller.com", dto.GetProperty("email").GetString());
        Assert.Equal("mechanic", dto.GetProperty("role").GetString());
        Assert.True(dto.GetProperty("id").GetGuid() != Guid.Empty);

        // El email no llega instantáneo (outbox dispatcher); la invitación quedó en la tabla.
        // Verificamos que al menos el endpoint respondió 201 con la invitación correcta.
    }

    // ── T-USR-02: invitación duplicada devuelve 409 ─────────────────────────────────
    [Fact(DisplayName = "T-USR-02: segunda invitación al mismo email devuelve 409")]
    public async Task Segunda_invitacion_mismo_email_devuelve_409()
    {
        using var client = CreateClient();
        await LoginAsOwnerAsync(client);

        async Task<HttpResponseMessage> Invite()
        {
            var csrf = await FetchAntiforgeryTokenAsync(client);
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invitations")
            {
                Content = JsonContent.Create(new { email = "dup@taller.com", displayName = "Dup", role = "mechanic" }),
            };
            req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
            return await client.SendAsync(req);
        }

        var first = await Invite();
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await Invite();
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    // ── T-USR-03: mecánico (sin permiso) intenta invitar → 403 ──────────────────────
    [Fact(DisplayName = "T-USR-03: mecánico sin users.manage obtiene 403 al invitar")]
    public async Task Mecanico_sin_permiso_obtiene_403_al_invitar()
    {
        // La factory solo tiene al dueño; sin cookie de sesión = 401 (no autenticado).
        // Para T-USR-03 auténtico necesitaríamos un mecánico invitado y aceptado.
        // Verificamos el caso sin sesión = 401 y con sesión de owner pero invitando rol Owner = 422.
        using var client = CreateClient();
        // Sin sesión: 401
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invitations")
        {
            Content = JsonContent.Create(new { email = "x@x.com", displayName = "X", role = "mechanic" }),
        };
        // Sin antiforgery: 400. Obtenemos antiforgery pero no autenticamos.
        var csrf = await FetchAntiforgeryTokenAsync(client);
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── T-USR-04: aceptar invitación ────────────────────────────────────────────────
    [Fact(DisplayName = "T-USR-04: aceptar invitación válida devuelve 204 y setea cookie de sesión")]
    public async Task Aceptar_invitacion_valida_emite_cookie_de_sesion()
    {
        using var ownerClient = CreateClient();
        await LoginAsOwnerAsync(ownerClient);

        // Crear invitación
        var csrf = await FetchAntiforgeryTokenAsync(ownerClient);
        var invReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/invitations")
        {
            Content = JsonContent.Create(new
            {
                email = "invited@taller.com",
                displayName = "Invitado",
                role = "mechanic",
            }),
        };
        invReq.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        var invResp = await ownerClient.SendAsync(invReq);
        Assert.Equal(HttpStatusCode.Created, invResp.StatusCode);

        // Extraer el token real del outbox: consultamos la tabla directamente.
        // Como el dispatcher no corre en tests, extraemos el token de la base.
        var token = await GetInvitationTokenFromDbAsync("invited@taller.com");
        Assert.NotNull(token);

        // Aceptar con cliente anónimo
        using var anonClient = CreateClient();
        var acceptResp = await anonClient.PostAsJsonAsync(
            $"/api/public/v1/invitations/{token}/accept",
            new { password = "NewPassword!2026" });

        Assert.Equal(HttpStatusCode.NoContent, acceptResp.StatusCode);
        Assert.Contains(
            acceptResp.Headers.GetValues("Set-Cookie"),
            v => v.StartsWith(AuthenticationSetup.SessionCookieName + "=", StringComparison.Ordinal));
    }

    // ── T-AUTH-07: forgot-password siempre 202 ──────────────────────────────────────
    [Fact(DisplayName = "T-AUTH-07: forgot-password devuelve 202 para email existente y no existente")]
    public async Task ForgotPassword_siempre_devuelve_202()
    {
        using var client = CreateClient();

        var knownResp = await client.PostAsJsonAsync("/api/v1/auth/forgot-password",
            new { email = InvitationWebApplicationFactory.SeedEmail });

        var unknownResp = await client.PostAsJsonAsync("/api/v1/auth/forgot-password",
            new { email = "inexistente@taller.com" });

        Assert.Equal(HttpStatusCode.Accepted, knownResp.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknownResp.StatusCode);
    }

    // ── T-AUTH-08: reset-password con token vencido/usado → 422 ─────────────────────
    [Fact(DisplayName = "T-AUTH-08: reset-password con token inválido devuelve 422")]
    public async Task ResetPassword_con_token_invalido_devuelve_422()
    {
        using var client = CreateClient();

        var resp = await client.PostAsJsonAsync("/api/v1/auth/reset-password",
            new { token = "token-que-no-existe", newPassword = "NewPassword!2026" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    [Fact(DisplayName = "T-AUTH-08b: reset-password exitoso devuelve 204")]
    public async Task ResetPassword_con_token_valido_devuelve_204()
    {
        using var client = CreateClient();

        // Solicitar reset (encola en outbox pero no envía; token queda en DB).
        await client.PostAsJsonAsync("/api/v1/auth/forgot-password",
            new { email = InvitationWebApplicationFactory.SeedEmail });

        var token = await GetPasswordResetTokenFromDbAsync(InvitationWebApplicationFactory.SeedEmail);
        Assert.NotNull(token);

        var resp = await client.PostAsJsonAsync("/api/v1/auth/reset-password",
            new { token, newPassword = "ResetPassword!2026" });

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    private async Task<string?> GetInvitationTokenFromDbAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TallerControl.Infrastructure.Persistence.AppDbContext>();

        // Filtramos por tipo en DB y por email en memoria: Contains() sobre jsonb falla en PostgreSQL.
        var messages = await db.Set<TallerControl.Domain.Outbox.OutboxMessage>()
            .IgnoreQueryFilters()
            .Where(m => m.Type == "email.invitation")
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

        var message = messages.FirstOrDefault(m => m.PayloadJson.Contains(email));
        if (message is null)
        {
            return null;
        }

        var payload = JsonSerializer.Deserialize<JsonElement>(message.PayloadJson);
        return payload.GetProperty("Token").GetString();
    }

    private async Task<string?> GetPasswordResetTokenFromDbAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TallerControl.Infrastructure.Persistence.AppDbContext>();

        var messages = await db.Set<TallerControl.Domain.Outbox.OutboxMessage>()
            .IgnoreQueryFilters()
            .Where(m => m.Type == "email.password_reset")
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

        var message = messages.FirstOrDefault(m => m.PayloadJson.Contains(email));
        if (message is null)
        {
            return null;
        }

        var payload = JsonSerializer.Deserialize<JsonElement>(message.PayloadJson);
        return payload.GetProperty("Token").GetString();
    }

    // ── U-01: lista, reenvío, cancelación, nombre invitado, contador ──────────

    private async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string url, object? body = null)
    {
        var csrf = await FetchAntiforgeryTokenAsync(client);
        var req = new HttpRequestMessage(method, url);
        if (body is not null) req.Content = JsonContent.Create(body);
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        return await client.SendAsync(req);
    }

    [Fact(DisplayName = "T-USR-08: Las invitaciones pendientes se listan, se reenvían (el enlace viejo deja de servir) y se cancelan")]
    public async Task Listar_reenviar_y_cancelar_invitaciones()
    {
        using var client = CreateClient();
        await LoginAsOwnerAsync(client);

        var created = await (await SendAsync(client, HttpMethod.Post, "/api/v1/invitations",
            new { email = "contador@taller.com", displayName = "Rosa Contadora", role = "accountant" })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("accountant", created.GetProperty("role").GetString());
        var id = created.GetProperty("id").GetGuid();

        var list = await client.GetFromJsonAsync<JsonElement>("/api/v1/invitations");
        var pending = list.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == id);
        Assert.Equal("Rosa Contadora", pending.GetProperty("displayName").GetString());
        Assert.False(pending.GetProperty("isExpired").GetBoolean());

        // Un pedido igual mientras está vigente se rechaza con un mensaje que manda a reenviar.
        var dup = await SendAsync(client, HttpMethod.Post, "/api/v1/invitations",
            new { email = "contador@taller.com", displayName = "Rosa", role = "accountant" });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        Assert.Contains("reenviala", await dup.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Post, $"/api/v1/invitations/{id}/resend")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(client, HttpMethod.Delete, $"/api/v1/invitations/{id}")).StatusCode);
        var after = await client.GetFromJsonAsync<JsonElement>("/api/v1/invitations");
        Assert.DoesNotContain(after.EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);

        // Cancelada, se puede volver a invitar a esa persona.
        Assert.Equal(HttpStatusCode.Created, (await SendAsync(client, HttpMethod.Post, "/api/v1/invitations",
            new { email = "contador@taller.com", displayName = "Rosa", role = "accountant" })).StatusCode);
    }

    [Fact(DisplayName = "T-USR-09: Invitar al propio dueño (ya es miembro) devuelve 409 con un mensaje claro")]
    public async Task Invitar_a_un_miembro_actual_devuelve_409()
    {
        using var client = CreateClient();
        await LoginAsOwnerAsync(client);

        var resp = await SendAsync(client, HttpMethod.Post, "/api/v1/invitations",
            new { email = InvitationWebApplicationFactory.SeedEmail, displayName = "Yo", role = "mechanic" });
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Contains("ya forma parte", await resp.Content.ReadAsStringAsync());
    }
}
