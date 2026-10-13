using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TallerControl.Api.Identity;
using TallerControl.Api.IntegrationTests.Persistence;

namespace TallerControl.Api.IntegrationTests.Users;

/// <summary>
/// E2 bloque 4: gestión de miembros del taller.
/// T-PERM-01, T-USR-05, T-USR-06, T-USR-07 (doc 08 §4, doc 05 §2, REQ-03).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class UsersEndpointsTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private UsersWebApplicationFactory _factory = null!;

    public UsersEndpointsTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        _factory = await UsersWebApplicationFactory.CreateAsync(_postgres);
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

    private async Task LoginAsync(HttpClient client, string email, string password)
    {
        var csrf = await FetchAntiforgeryTokenAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email, password, rememberMe = false }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    private async Task<HttpResponseMessage> PatchMembershipAsync(
        HttpClient client,
        Guid membershipId,
        object body)
    {
        // Obtenemos la versión actual de la membresía mediante el listado.
        var version = await GetMemberVersionAsync(client, membershipId);

        var csrf = await FetchAntiforgeryTokenAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/users/{membershipId}")
        {
            Content = JsonContent.Create(body),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        req.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        return await client.SendAsync(req);
    }

    private async Task<uint> GetMemberVersionAsync(HttpClient client, Guid membershipId)
    {
        var resp = await client.GetAsync("/api/v1/users");
        resp.EnsureSuccessStatusCode();
        var list = await resp.Content.ReadFromJsonAsync<JsonElement[]>();
        var member = list!.First(m => m.GetProperty("membershipId").GetGuid() == membershipId);
        return member.GetProperty("version").GetUInt32();
    }

    // ── T-PERM-01: administrativo lista usuarios → 403 ──────────────────────────
    [Fact(DisplayName = "T-PERM-01: administrativo sin users.manage obtiene 403 al listar miembros")]
    public async Task Administrativo_sin_users_manage_obtiene_403_al_listar()
    {
        using var client = CreateClient();
        await LoginAsync(client, UsersWebApplicationFactory.AdminEmail, UsersWebApplicationFactory.AdminPassword);

        var resp = await client.GetAsync("/api/v1/users");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ── Owner lista miembros → 200 + lista los 3 miembros ───────────────────────
    [Fact(DisplayName = "Owner con users.manage lista miembros y recibe 200")]
    public async Task Owner_lista_miembros_recibe_200()
    {
        using var client = CreateClient();
        await LoginAsync(client, UsersWebApplicationFactory.Owner1Email, UsersWebApplicationFactory.Owner1Password);

        var resp = await client.GetAsync("/api/v1/users");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var list = await resp.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(list);
        Assert.Equal(3, list.Length);
    }

    // ── T-USR-05: quitar el rol al único dueño → 422 ────────────────────────────
    [Fact(DisplayName = "T-USR-05: no se puede quitar el rol al último dueño activo → 422")]
    public async Task No_se_puede_quitar_rol_al_unico_dueno()
    {
        // Desactivamos al owner2 primero para que solo quede owner1 como owner activo.
        using var client = CreateClient();
        await LoginAsync(client, UsersWebApplicationFactory.Owner1Email, UsersWebApplicationFactory.Owner1Password);

        // Paso 1: Deshabilitar owner2 (hay 2 owners, es válido).
        var disableResp = await PatchMembershipAsync(
            client,
            _factory.Owner2MembershipId,
            new { status = "disabled" });
        Assert.Equal(HttpStatusCode.OK, disableResp.StatusCode);

        // Paso 2: Intentar cambiar el rol del único dueño activo (owner1) → 422.
        var changeResp = await PatchMembershipAsync(
            client,
            _factory.Owner1MembershipId,
            new { role = "admin_staff" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, changeResp.StatusCode);
    }

    // ── T-USR-06: cambiar el rol invalida la sesión ──────────────────────────────
    [Fact(DisplayName = "T-USR-06: cambiar el rol de un usuario invalida su sesión activa")]
    public async Task Cambiar_rol_invalida_sesion_del_usuario()
    {
        // Owner2 inicia sesión.
        using var owner2Client = CreateClient();
        await LoginAsync(
            owner2Client,
            UsersWebApplicationFactory.Owner2Email,
            UsersWebApplicationFactory.Owner2Password);

        // Owner1 degrada a Owner2 a admin_staff (hay 2 owners activos).
        using var owner1Client = CreateClient();
        await LoginAsync(
            owner1Client,
            UsersWebApplicationFactory.Owner1Email,
            UsersWebApplicationFactory.Owner1Password);

        var patchResp = await PatchMembershipAsync(
            owner1Client,
            _factory.Owner2MembershipId,
            new { role = "admin_staff" });
        Assert.Equal(HttpStatusCode.OK, patchResp.StatusCode);

        // La siguiente solicitud autenticada de Owner2 debe ser 401.
        var meResp = await owner2Client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meResp.StatusCode);
    }

    // ── T-USR-07: desactivar conserva el nombre histórico ───────────────────────
    [Fact(DisplayName = "T-USR-07: desactivar un usuario conserva su nombre en la lista")]
    public async Task Desactivar_usuario_conserva_nombre_en_lista()
    {
        using var client = CreateClient();
        await LoginAsync(client, UsersWebApplicationFactory.Owner1Email, UsersWebApplicationFactory.Owner1Password);

        // Deshabilitar admin (hay solo 1 admin, no es owner → siempre válido).
        var patchResp = await PatchMembershipAsync(
            client,
            _factory.AdminMembershipId,
            new { status = "disabled" });
        Assert.Equal(HttpStatusCode.OK, patchResp.StatusCode);

        // Listar miembros y verificar que el nombre del admin sigue presente.
        var listResp = await client.GetAsync("/api/v1/users");
        listResp.EnsureSuccessStatusCode();

        var list = await listResp.Content.ReadFromJsonAsync<JsonElement[]>();
        var admin = list!.FirstOrDefault(m =>
            m.GetProperty("email").GetString() == UsersWebApplicationFactory.AdminEmail);

        Assert.False(admin.ValueKind == JsonValueKind.Undefined, "El admin desactivado debe seguir en la lista.");
        Assert.Equal(
            UsersWebApplicationFactory.AdminDisplayName,
            admin.GetProperty("displayName").GetString());
        Assert.Equal("disabled", admin.GetProperty("status").GetString());
    }
}
