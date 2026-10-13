using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using TallerControl.Api.Identity;
using TallerControl.Api.IntegrationTests.Persistence;
using TallerControl.Domain.Users;

namespace TallerControl.Api.IntegrationTests.Permissions;

/// <summary>
/// T-PERM-ALL y T-ISO-ALL (doc 08): pruebas generadas por reflexión sobre las rutas mapeadas, no
/// una lista a mano. Cualquier endpoint nuevo bajo <c>/api/v1/*</c> con una política <c>perm:*</c>
/// entra solo a T-PERM-ALL; uno con parámetro de ruta sin resolver hace fallar a T-ISO-ALL con un
/// mensaje explícito, para forzar a enseñarle qué recurso real usar.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PermissionMatrixTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private PermissionMatrixWebApplicationFactory _factory = null!;

    public PermissionMatrixTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        _factory = await PermissionMatrixWebApplicationFactory.CreateAsync(_postgres);
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    private sealed record PermissionEndpoint(string Method, string RoutePattern, string Permission);

    private static readonly Regex RouteParam = new(@"\{[^}]+\}", RegexOptions.Compiled);

    /// <summary>Todo endpoint bajo /api/v1/* con una política perm:* (doc 07 §3). Excluye lo público
    /// y lo que solo exige sesión sin un permiso puntual (p. ej. /auth/me).</summary>
    private IReadOnlyList<PermissionEndpoint> DiscoverPermissionEndpoints()
    {
        var endpoints = _factory.Services.GetServices<EndpointDataSource>()
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>();

        var found = new List<PermissionEndpoint>();

        foreach (var endpoint in endpoints)
        {
            var route = endpoint.RoutePattern.RawText ?? string.Empty;
            if (!route.StartsWith("/api/v1/", StringComparison.Ordinal))
            {
                continue;
            }

            var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;
            if (methods is null || methods.Count == 0)
            {
                continue;
            }

            var permissionPolicy = endpoint.Metadata.OfType<IAuthorizeData>()
                .Select(a => a.Policy)
                .FirstOrDefault(p => p is not null && p.StartsWith("perm:", StringComparison.Ordinal));

            if (permissionPolicy is null)
            {
                continue;
            }

            var permission = permissionPolicy["perm:".Length..];
            foreach (var method in methods)
            {
                found.Add(new PermissionEndpoint(method, route, permission));
            }
        }

        return found;
    }

    private static string ResolveWithRandomId(string pattern) => RouteParam.Replace(pattern, Guid.NewGuid().ToString());

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
                password = PermissionMatrixWebApplicationFactory.Password,
                rememberMe = false,
            }),
        };
        req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    /// <summary>
    /// Pega al endpoint sin preocuparse por el cuerpo exacto: solo nos interesa si la autorización
    /// lo dejó pasar (cualquier código que no sea 403) o no (403). El CSRF y un If-Match "de relleno"
    /// evitan que el antiforgery filter o la falta de cabecera corten antes de llegar al caso de uso.
    /// DELETE también lleva CSRF y body (reason) para pasar el AntiforgeryEndpointFilter de las rutas
    /// de clientes y vehículos y llegar al handler donde el filtro de taller devuelve 404 (T-ISO-ALL).
    /// </summary>
    private static async Task<HttpStatusCode> HitAsync(HttpClient client, string method, string route, string csrf)
    {
        var req = new HttpRequestMessage(new HttpMethod(method), route);
        if (method is "POST" or "PUT" or "PATCH" or "DELETE")
        {
            req.Content = JsonContent.Create(new { reason = "perm-matrix-test" });
            req.Headers.TryAddWithoutValidation("If-Match", "\"0\"");
            req.Headers.Add(AuthenticationSetup.AntiforgeryHeaderName, csrf);
        }

        var resp = await client.SendAsync(req);
        return resp.StatusCode;
    }

    // ── T-PERM-ALL: cada endpoint con permiso puntual, contra los tres roles base ──────
    [Theory(DisplayName = "T-PERM-ALL: matriz de permisos (doc 07 §3) 100% coincidente")]
    [InlineData(nameof(TenantRole.Owner), PermissionMatrixWebApplicationFactory.OwnerAEmail)]
    [InlineData(nameof(TenantRole.AdminStaff), PermissionMatrixWebApplicationFactory.AdminAEmail)]
    [InlineData(nameof(TenantRole.Mechanic), PermissionMatrixWebApplicationFactory.MechanicAEmail)]
    public async Task Cada_endpoint_respeta_el_permiso_del_rol(string roleName, string email)
    {
        var role = Enum.Parse<TenantRole>(roleName);
        var allowed = Permission.BaseFor(role);

        var endpoints = DiscoverPermissionEndpoints();
        Assert.NotEmpty(endpoints);

        using var client = CreateClient();
        await LoginAsync(client, email);
        var csrf = await FetchCsrfAsync(client);

        var failures = new List<string>();
        foreach (var endpoint in endpoints)
        {
            var status = await HitAsync(client, endpoint.Method, ResolveWithRandomId(endpoint.RoutePattern), csrf);
            var shouldBeAllowed = allowed.Contains(endpoint.Permission);
            var wasForbidden = status == HttpStatusCode.Forbidden;

            if (shouldBeAllowed == wasForbidden)
            {
                failures.Add(
                    $"{endpoint.Method} {endpoint.RoutePattern} (perm={endpoint.Permission}): rol {role} " +
                    $"{(shouldBeAllowed ? "debería pasar el permiso" : "no debería tener el permiso")}, respondió {(int)status}.");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    // ── T-ISO-ALL: todo endpoint con {id} × recurso de otro taller → 404 ───────────────
    [Fact(DisplayName = "T-ISO-ALL: todos los endpoints con {id} responden 404 ante un recurso de otro taller")]
    public async Task Cada_endpoint_con_id_devuelve_404_para_recurso_de_otro_taller()
    {
        var all = DiscoverPermissionEndpoints();

        // Un endpoint es "resolvable" si su ruta con parámetros tiene un recurso sembrado del Taller A
        // que el test puede sustituir para probar el aislamiento desde el Taller B.
        // Al agregar endpoints nuevos con {id}, sumar el recurso sembrado en la factory
        // y el caso de resolución aquí; si falta, el Assert.True(unresolved == 0) falla con mensaje.
        var resolvable = all
            .Where(e => RouteParam.IsMatch(e.RoutePattern) && (
                e.RoutePattern.Contains("{membershipId", StringComparison.Ordinal) ||
                e.RoutePattern.Contains("/customers/", StringComparison.Ordinal) ||
                e.RoutePattern.Contains("/vehicles/", StringComparison.Ordinal) ||
                e.RoutePattern.Contains("/catalog/", StringComparison.Ordinal)))
            .ToList();

        var unresolved = all
            .Where(e => RouteParam.IsMatch(e.RoutePattern))
            .Except(resolvable)
            .ToList();

        Assert.True(
            unresolved.Count == 0,
            "T-ISO-ALL no sabe qué recurso usar para: " +
            string.Join(", ", unresolved.Select(e => $"{e.Method} {e.RoutePattern}")) +
            ". Agregá un recurso sembrado en PermissionMatrixWebApplicationFactory y resolvé su ruta acá.");

        Assert.NotEmpty(resolvable);

        using var client = CreateClient();
        await LoginAsync(client, PermissionMatrixWebApplicationFactory.OwnerBEmail);
        var csrf = await FetchCsrfAsync(client);

        var failures = new List<string>();
        foreach (var endpoint in resolvable)
        {
            var route = ResolveRoute(endpoint.RoutePattern);
            var status = await HitAsync(client, endpoint.Method, route, csrf);
            if (status != HttpStatusCode.NotFound)
            {
                failures.Add($"{endpoint.Method} {route}: esperaba 404, respondió {(int)status}.");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>
    /// Sustituye los parámetros de ruta por el ID del recurso sembrado del Taller A que corresponde
    /// al prefijo de la ruta. Cada nuevo módulo con endpoints <c>{id}</c> debe agregar su caso acá.
    /// </summary>
    private string ResolveRoute(string pattern)
    {
        if (pattern.Contains("{membershipId", StringComparison.Ordinal))
        {
            return pattern
                .Replace("{membershipId:guid}", _factory.ForeignMembershipId.ToString())
                .Replace("{membershipId}", _factory.ForeignMembershipId.ToString());
        }

        if (pattern.Contains("/customers/", StringComparison.Ordinal))
        {
            return RouteParam.Replace(pattern, _factory.ForeignCustomerId.ToString());
        }

        if (pattern.Contains("/vehicles/", StringComparison.Ordinal))
        {
            return RouteParam.Replace(pattern, _factory.ForeignVehicleId.ToString());
        }

        if (pattern.Contains("/catalog/", StringComparison.Ordinal))
        {
            return RouteParam.Replace(pattern, _factory.ForeignCatalogItemId.ToString());
        }

        // Fallback: no debería llegar aquí si resolvable está correctamente definido
        return RouteParam.Replace(pattern, Guid.NewGuid().ToString());
    }
}
