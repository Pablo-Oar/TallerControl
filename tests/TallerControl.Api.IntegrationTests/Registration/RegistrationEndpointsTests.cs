using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TallerControl.Api.Identity;
using TallerControl.Api.IntegrationTests.Persistence;
using TallerControl.Domain.Outbox;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Api.IntegrationTests.Registration;

/// <summary>
/// E10: auto-registro de talleres (D-22).
/// T-REG-01..06 (doc 08 §4).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RegistrationEndpointsTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private RegistrationWebApplicationFactory _factory = null!;

    public RegistrationEndpointsTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        _factory = await RegistrationWebApplicationFactory.CreateAsync(_postgres);
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    // ── helpers ────────────────────────────────────────────────────────────────────

    private static HttpContent RegisterBody(
        string workshopName = "Taller Test",
        string displayName = "Carlos Test",
        string email = "carlos@test.com",
        string password = "SecurePass!2026")
        => JsonContent.Create(new { workshopName, displayName, email, password });

    private async Task<string?> GetVerificationTokenFromDbAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var messages = await db.Set<OutboxMessage>()
            .IgnoreQueryFilters()
            .Where(m => m.Type == "email.email_verification")
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

        var message = messages.FirstOrDefault(m => m.PayloadJson.Contains(email));
        if (message is null) return null;

        var payload = JsonSerializer.Deserialize<JsonElement>(message.PayloadJson);
        return payload.GetProperty("Token").GetString();
    }

    private async Task<string> FetchAntiforgeryTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/auth/antiforgery-token");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("token").GetString()!;
    }

    // ── T-REG-01: registro exitoso ─────────────────────────────────────────────────

    [Fact(DisplayName = "T-REG-01: registro válido responde 201 y encola email de verificación")]
    public async Task Registro_valido_responde_201_y_encola_verificacion()
    {
        using var client = _factory.CreateClient();

        var resp = await client.PostAsync("/api/public/v1/register", RegisterBody());

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("carlos@test.com", body.GetProperty("email").GetString());

        var token = await GetVerificationTokenFromDbAsync("carlos@test.com");
        Assert.NotNull(token);
    }

    // ── T-REG-02: email ya existente responde igual (sin revelar existencia) ────────

    [Fact(DisplayName = "T-REG-02: email ya registrado responde 201 idéntico (sin revelar existencia)")]
    public async Task Email_duplicado_responde_201_identico()
    {
        using var client = _factory.CreateClient();

        const string email = "dup@test.com";
        var body = RegisterBody(email: email);

        var first = await client.PostAsync("/api/public/v1/register", body);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsync(
            "/api/public/v1/register",
            RegisterBody(email: email, workshopName: "Otro Taller"));

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(
            firstBody.GetProperty("email").GetString(),
            secondBody.GetProperty("email").GetString());
    }

    // ── T-REG-03: contraseña débil → 422 ──────────────────────────────────────────

    [Fact(DisplayName = "T-REG-03: contraseña menor a 8 caracteres devuelve 422")]
    public async Task Password_debil_devuelve_422()
    {
        using var client = _factory.CreateClient();

        var resp = await client.PostAsync(
            "/api/public/v1/register",
            RegisterBody(email: "weak@test.com", password: "abc"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    // ── T-REG-04: verificación exitosa de email ────────────────────────────────────

    [Fact(DisplayName = "T-REG-04: verificar email con token válido responde 200 success=true")]
    public async Task Verificar_email_con_token_valido_responde_200_success_true()
    {
        using var client = _factory.CreateClient();
        const string email = "verify@test.com";

        await client.PostAsync("/api/public/v1/register", RegisterBody(email: email));

        var token = await GetVerificationTokenFromDbAsync(email);
        Assert.NotNull(token);

        var verifyResp = await client.GetAsync(
            $"/api/public/v1/verify-email?token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.OK, verifyResp.StatusCode);

        var body = await verifyResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("success").GetBoolean());
    }

    // ── T-REG-05: token inválido devuelve 400 ─────────────────────────────────────

    [Fact(DisplayName = "T-REG-05: verificar email con token inválido devuelve 400 success=false")]
    public async Task Verificar_email_con_token_invalido_devuelve_400()
    {
        using var client = _factory.CreateClient();

        var resp = await client.GetAsync("/api/public/v1/verify-email?token=token-invalido");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("success").GetBoolean());
    }

    // ── T-REG-06: login bloqueado hasta verificar (D-22) ──────────────────────────

    [Fact(DisplayName = "T-REG-06: login con email no verificado devuelve 401 con code=email_not_verified")]
    public async Task Login_con_email_no_verificado_devuelve_401_email_not_verified()
    {
        using var client = _factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
            });

        const string email = "unverified@test.com";
        await client.PostAsync(
            "/api/public/v1/register",
            RegisterBody(email: email, password: "SecurePass!2026"));

        // Intentar login antes de verificar el email
        var csrf = await FetchAntiforgeryTokenAsync(client);
        var loginReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email, password = "SecurePass!2026", rememberMe = false }),
        };
        loginReq.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);

        var loginResp = await client.SendAsync(loginReq);

        Assert.Equal(HttpStatusCode.Unauthorized, loginResp.StatusCode);

        var body = await loginResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("email_not_verified", body.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "T-REG-07: reenviar verificación responde 202 uniforme, emite un enlace nuevo y respeta el intervalo")]
    public async Task Reenviar_verificacion()
    {
        using var client = _factory.CreateClient();
        const string email = "resend@test.com";
        await client.PostAsync("/api/public/v1/register", RegisterBody(email: email));

        // Dentro del minuto siguiente al registro no se reenvía, pero la respuesta es igual.
        var first = await client.PostAsJsonAsync("/api/public/v1/resend-verification", new { email });
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);

        // Un email que no existe responde igual (no revela quién tiene cuenta).
        var unknown = await client.PostAsJsonAsync("/api/public/v1/resend-verification", new { email = "nadie@test.com" });
        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sent = (await db.Set<OutboxMessage>().IgnoreQueryFilters()
            .Where(m => m.Type == "email.email_verification").ToListAsync())
            .Count(m => m.PayloadJson.Contains(email));
        Assert.Equal(1, sent);
    }
}
