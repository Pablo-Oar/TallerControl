using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using TallerControl.Api.Identity;
using TallerControl.Api.IntegrationTests.Persistence;
using TallerControl.Domain.Users;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Api.IntegrationTests.Auth;

/// <summary>
/// E2 bloque 2: autenticación por cookie (<c>POST /auth/login</c>, <c>POST /auth/logout</c>,
/// <c>GET /auth/me</c>). Cubre T-AUTH-01..06 (doc 08 §4, doc 05 §2).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AuthEndpointsTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private AuthWebApplicationFactory _factory = null!;

    public AuthEndpointsTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        _factory = await AuthWebApplicationFactory.CreateAsync(_postgres);
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

    private static HttpRequestMessage LoginRequest(string email, string password, string antiforgeryToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new
            {
                email,
                password,
                rememberMe = false,
            }),
        };
        request.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, antiforgeryToken);
        return request;
    }

    [Fact(DisplayName = "T-AUTH-01: login correcto responde 204 y setea la cookie tc_session")]
    public async Task Login_correcto_setea_cookie_tc_session()
    {
        using var client = CreateClient();
        var token = await FetchAntiforgeryTokenAsync(client);

        var response = await client.SendAsync(LoginRequest(
            AuthWebApplicationFactory.SeedEmail,
            AuthWebApplicationFactory.SeedPassword,
            token));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains(
            response.Headers.GetValues("Set-Cookie"),
            v => v.StartsWith(AuthenticationSetup.SessionCookieName + "=", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "T-AUTH-02: contraseña incorrecta y email inexistente devuelven el mismo 401 genérico")]
    public async Task Login_falla_de_forma_identica_para_password_mala_o_email_inexistente()
    {
        using var client = CreateClient();
        var token = await FetchAntiforgeryTokenAsync(client);

        var wrongPassword = await client.SendAsync(LoginRequest(
            AuthWebApplicationFactory.SeedEmail,
            "Password-Que-No-Es",
            token));

        var token2 = await FetchAntiforgeryTokenAsync(client);
        var unknownEmail = await client.SendAsync(LoginRequest(
            "fantasma@taller.com",
            "Cualquier-Password",
            token2));

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);

        // El cuerpo trae el mismo título y detalle en ambos casos (el traceId por request difiere).
        var wrongBody = await wrongPassword.Content.ReadFromJsonAsync<JsonElement>();
        var unknownBody = await unknownEmail.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(wrongBody.GetProperty("title").GetString(), unknownBody.GetProperty("title").GetString());
        Assert.Equal(wrongBody.GetProperty("detail").GetString(), unknownBody.GetProperty("detail").GetString());
    }

    [Fact(DisplayName = "T-AUTH-03: tras 5 fallos la cuenta queda bloqueada y responde 423")]
    public async Task Seis_fallos_devuelven_423_bloqueado()
    {
        using var client = CreateClient();

        for (var attempt = 0; attempt < User.MaxFailedAttempts; attempt++)
        {
            var token = await FetchAntiforgeryTokenAsync(client);
            var response = await client.SendAsync(LoginRequest(
                AuthWebApplicationFactory.SeedEmail,
                "Password-Mal",
                token));
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var finalToken = await FetchAntiforgeryTokenAsync(client);
        var lockedResponse = await client.SendAsync(LoginRequest(
            AuthWebApplicationFactory.SeedEmail,
            AuthWebApplicationFactory.SeedPassword,
            finalToken));

        Assert.Equal((HttpStatusCode)423, lockedResponse.StatusCode);
    }

    [Fact(DisplayName = "T-AUTH-04: POST sin cabecera antiforgery responde 400")]
    public async Task Login_sin_cabecera_antiforgery_devuelve_400()
    {
        using var client = CreateClient();
        // Nos aseguramos de tener la cookie antiforgery para aislar el fallo a la cabecera.
        await FetchAntiforgeryTokenAsync(client);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new
            {
                email = AuthWebApplicationFactory.SeedEmail,
                password = AuthWebApplicationFactory.SeedPassword,
                rememberMe = false,
            }),
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "T-AUTH-05: logout rota el security_stamp y la cookie deja de servir")]
    public async Task Logout_invalida_la_cookie_de_sesion()
    {
        using var client = CreateClient();
        var loginToken = await FetchAntiforgeryTokenAsync(client);
        var login = await client.SendAsync(LoginRequest(
            AuthWebApplicationFactory.SeedEmail,
            AuthWebApplicationFactory.SeedPassword,
            loginToken));
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);

        var meBefore = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, meBefore.StatusCode);

        var stampBefore = await ReadSecurityStampAsync();

        var logoutToken = await FetchAntiforgeryTokenAsync(client);
        var logout = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        logout.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, logoutToken);
        var logoutResponse = await client.SendAsync(logout);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var stampAfter = await ReadSecurityStampAsync();
        Assert.NotEqual(stampBefore, stampAfter);

        var meAfter = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meAfter.StatusCode);
    }

    [Fact(DisplayName = "T-AUTH-06: /auth/me devuelve rol owner y los permisos de la membresía")]
    public async Task Me_devuelve_rol_y_permisos_de_la_membresia()
    {
        using var client = CreateClient();
        var token = await FetchAntiforgeryTokenAsync(client);
        var login = await client.SendAsync(LoginRequest(
            AuthWebApplicationFactory.SeedEmail,
            AuthWebApplicationFactory.SeedPassword,
            token));
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);

        var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");

        Assert.Equal(AuthWebApplicationFactory.SeedEmail, me.GetProperty("user").GetProperty("email").GetString());
        Assert.Equal(_factory.SeedTenantId, me.GetProperty("tenant").GetProperty("id").GetGuid());
        Assert.Equal("owner", me.GetProperty("role").GetString());
        var permissions = me.GetProperty("permissions").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains(Permission.SettingsManage, permissions);
        Assert.Contains(Permission.UsersManage, permissions);
    }

    private async Task<string> ReadSecurityStampAsync()
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        builder.UseTallerControlNpgsql(_factory.ConnectionString);
        await using var db = new AppDbContext(
            builder.Options,
            new TestTenantContext(),
            new TestActorContext(),
            TimeProvider.System);
        return await db.Set<User>()
            .Where(u => u.Id == _factory.SeedUserId)
            .Select(u => u.SecurityStamp)
            .SingleAsync();
    }
}
