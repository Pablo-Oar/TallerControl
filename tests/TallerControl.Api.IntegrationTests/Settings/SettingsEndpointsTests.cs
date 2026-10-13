using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TallerControl.Api.Identity;
using TallerControl.Api.IntegrationTests.Persistence;
using TallerControl.Application.Settings;

namespace TallerControl.Api.IntegrationTests.Settings;

/// <summary>
/// E2 bloque 6: configuración del taller.
/// T-SET-01, T-SET-02, T-SET-03 (doc 08, doc 05 §2.2, REQ-04).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SettingsEndpointsTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private SettingsWebApplicationFactory _factory = null!;

    public SettingsEndpointsTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        _factory = await SettingsWebApplicationFactory.CreateAsync(_postgres);
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

    private static async Task<string> FetchAntiforgeryTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/auth/antiforgery-token");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("token").GetString()!;
    }

    private static async Task LoginAsync(HttpClient client, string email, string password)
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

    private static async Task<JsonElement> GetSettingsAsync(HttpClient client)
    {
        var resp = await client.GetAsync("/api/v1/settings");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<HttpResponseMessage> PutSettingsAsync(HttpClient client, object body, string? ifMatch = null)
    {
        ifMatch ??= (await GetSettingsAsync(client)).GetProperty("version").GetString();

        var csrf = await FetchAntiforgeryTokenAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Put, "/api/v1/settings") { Content = JsonContent.Create(body) };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        req.Headers.TryAddWithoutValidation("If-Match", $"\"{ifMatch}\"");
        return await client.SendAsync(req);
    }

    private static object ValidUpdateBody(decimal defaultVatRate = 21m, int quoteValidityDays = 15) => new
    {
        name = "Taller Actualizado",
        legalName = "Taller Actualizado S.A.",
        taxId = "30-12345678-9",
        address = "Av. Siempre Viva 742",
        phone = "+5491122334455",
        timeZone = "America/Argentina/Buenos_Aires",
        defaultVatRate,
        quoteValidityDays,
    };

    private static async Task<HttpResponseMessage> PostLogoUploadUrlAsync(HttpClient client, string contentType, long size)
    {
        var csrf = await FetchAntiforgeryTokenAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/settings/logo/upload-url")
        {
            Content = JsonContent.Create(new { contentType, size }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        return await client.SendAsync(req);
    }

    private static async Task<HttpResponseMessage> PostLogoConfirmAsync(HttpClient client)
    {
        var csrf = await FetchAntiforgeryTokenAsync(client);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/settings/logo/confirm");
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        return await client.SendAsync(req);
    }

    // PNG real: firma de 8 bytes + un poco de relleno, alcanza para el chequeo de magic bytes.
    private static readonly byte[] PngBytes =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
    ];

    // ── T-SET-01: GET devuelve los valores por defecto del taller nuevo ────────
    [Fact(DisplayName = "T-SET-01: GET /settings devuelve los defaults del taller nuevo")]
    public async Task Get_devuelve_defaults_del_taller_nuevo()
    {
        using var client = CreateClient();
        await LoginAsync(client, SettingsWebApplicationFactory.OwnerEmail, SettingsWebApplicationFactory.OwnerPassword);

        var dto = await GetSettingsAsync(client);

        Assert.Equal(SettingsWebApplicationFactory.TenantName, dto.GetProperty("name").GetString());
        Assert.Equal("America/Argentina/Buenos_Aires", dto.GetProperty("timeZone").GetString());
        Assert.Equal(21m, dto.GetProperty("defaultVatRate").GetDecimal());
        Assert.Equal(15, dto.GetProperty("quoteValidityDays").GetInt32());
        Assert.Equal(JsonValueKind.Null, dto.GetProperty("logoUrl").ValueKind);

        // xmin es el id real de la transacción que insertó la fila, no un contador desde cero.
        var version = dto.GetProperty("version").GetString();
        Assert.True(SettingsVersion.TryParse(version!, out _, out _), $"Versión con formato inesperado: {version}");
    }

    [Fact(DisplayName = "Administrativo sin settings.manage obtiene 403")]
    public async Task Administrativo_sin_settings_manage_obtiene_403()
    {
        using var client = CreateClient();
        await LoginAsync(client, SettingsWebApplicationFactory.AdminEmail, SettingsWebApplicationFactory.AdminPassword);

        var resp = await client.GetAsync("/api/v1/settings");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact(DisplayName = "Owner edita la configuración y los cambios quedan guardados")]
    public async Task Owner_edita_configuracion_y_persiste()
    {
        using var client = CreateClient();
        await LoginAsync(client, SettingsWebApplicationFactory.OwnerEmail, SettingsWebApplicationFactory.OwnerPassword);

        var putResp = await PutSettingsAsync(client, ValidUpdateBody(defaultVatRate: 10.5m, quoteValidityDays: 30));
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        var dto = await GetSettingsAsync(client);
        Assert.Equal("Taller Actualizado", dto.GetProperty("name").GetString());
        Assert.Equal(10.5m, dto.GetProperty("defaultVatRate").GetDecimal());
        Assert.Equal(30, dto.GetProperty("quoteValidityDays").GetInt32());
    }

    // ── T-SET-02: alícuota de IVA fuera del conjunto permitido → 422 ────────────
    [Fact(DisplayName = "T-SET-02: IVA del 22% es rechazado con 422")]
    public async Task Iva_invalido_es_rechazado_con_422()
    {
        using var client = CreateClient();
        await LoginAsync(client, SettingsWebApplicationFactory.OwnerEmail, SettingsWebApplicationFactory.OwnerPassword);

        var resp = await PutSettingsAsync(client, ValidUpdateBody(defaultVatRate: 22m));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    [Fact(DisplayName = "PUT con If-Match vencido devuelve 412")]
    public async Task Put_con_version_vencida_devuelve_412()
    {
        using var client = CreateClient();
        await LoginAsync(client, SettingsWebApplicationFactory.OwnerEmail, SettingsWebApplicationFactory.OwnerPassword);

        var current = await GetSettingsAsync(client);
        SettingsVersion.TryParse(
            current.GetProperty("version").GetString()!, out var tenantVersion, out var settingsVersion);

        // xmin es el id de transacción real: sumar un salto grande garantiza que no coincida.
        var staleVersion = SettingsVersion.Format(tenantVersion + 1000, settingsVersion + 1000);

        var resp = await PutSettingsAsync(client, ValidUpdateBody(), ifMatch: staleVersion);

        Assert.Equal(HttpStatusCode.PreconditionFailed, resp.StatusCode);
    }

    // ── T-SET-03: logo que supera el tamaño máximo (5 MB, V2) → 422 ────────────
    [Fact(DisplayName = "T-SET-03: logo de 6 MB es rechazado con 422")]
    public async Task Logo_de_6mb_es_rechazado_con_422()
    {
        using var client = CreateClient();
        await LoginAsync(client, SettingsWebApplicationFactory.OwnerEmail, SettingsWebApplicationFactory.OwnerPassword);

        var resp = await PostLogoUploadUrlAsync(client, "image/png", 6 * 1024 * 1024);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    [Fact(DisplayName = "Tipo de archivo no permitido para el logo es rechazado con 422")]
    public async Task Logo_con_tipo_no_permitido_es_rechazado_con_422()
    {
        using var client = CreateClient();
        await LoginAsync(client, SettingsWebApplicationFactory.OwnerEmail, SettingsWebApplicationFactory.OwnerPassword);

        var resp = await PostLogoUploadUrlAsync(client, "application/pdf", 1024);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    [Fact(DisplayName = "upload-url devuelve una clave determinística por taller")]
    public async Task Upload_url_devuelve_clave_deterministica()
    {
        using var client = CreateClient();
        await LoginAsync(client, SettingsWebApplicationFactory.OwnerEmail, SettingsWebApplicationFactory.OwnerPassword);

        var resp = await PostLogoUploadUrlAsync(client, "image/png", 1024);
        resp.EnsureSuccessStatusCode();

        var dto = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal($"tenants/{_factory.TenantId}/logo", dto.GetProperty("fileKey").GetString());
    }

    [Fact(DisplayName = "Confirmar sin haber subido el archivo devuelve 422")]
    public async Task Confirmar_sin_archivo_subido_devuelve_422()
    {
        using var client = CreateClient();
        await LoginAsync(client, SettingsWebApplicationFactory.OwnerEmail, SettingsWebApplicationFactory.OwnerPassword);

        var resp = await PostLogoConfirmAsync(client);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    [Fact(DisplayName = "Confirmar con bytes que no coinciden con el tipo declarado devuelve 422")]
    public async Task Confirmar_con_magic_bytes_invalidos_devuelve_422()
    {
        using var client = CreateClient();
        await LoginAsync(client, SettingsWebApplicationFactory.OwnerEmail, SettingsWebApplicationFactory.OwnerPassword);

        var fileKey = $"tenants/{_factory.TenantId}/logo";
        _factory.Storage.Seed(fileKey, "image/png", "esto no es una imagen"u8.ToArray());

        var resp = await PostLogoConfirmAsync(client);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    [Fact(DisplayName = "Confirmar un logo válido lo deja disponible en GET /settings")]
    public async Task Confirmar_logo_valido_queda_disponible()
    {
        using var client = CreateClient();
        await LoginAsync(client, SettingsWebApplicationFactory.OwnerEmail, SettingsWebApplicationFactory.OwnerPassword);

        var fileKey = $"tenants/{_factory.TenantId}/logo";
        _factory.Storage.Seed(fileKey, "image/png", PngBytes);

        var confirmResp = await PostLogoConfirmAsync(client);
        Assert.Equal(HttpStatusCode.OK, confirmResp.StatusCode);

        var dto = await GetSettingsAsync(client);
        Assert.NotEqual(JsonValueKind.Null, dto.GetProperty("logoUrl").ValueKind);
        Assert.Contains(fileKey, dto.GetProperty("logoUrl").GetString());
    }
}
