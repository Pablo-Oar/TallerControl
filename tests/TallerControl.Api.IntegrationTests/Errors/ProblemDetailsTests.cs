using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

namespace TallerControl.Api.IntegrationTests.Errors;

/// <summary>
/// Formato uniforme de errores RFC 7807 con <c>traceId</c> (doc 02 §9 y doc 05 §1). Se levanta un
/// host mínimo con un endpoint por tipo de excepción, para probar la traducción sin depender de
/// endpoints de negocio (que llegan en etapas posteriores).
/// </summary>
[Collection(LogCaptureCollection.Name)]
public sealed class ProblemDetailsTests : IAsyncLifetime
{
    private WebApplication? _app;

    public async Task InitializeAsync() => _app = await TestHost.StartAsync();

    public async Task DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private HttpClient Client() => _app!.GetTestClient();

    [Fact]
    public async Task Una_excepcion_inesperada_responde_500_con_problem_json_y_traceId_pero_sin_detalles()
    {
        var response = await Client().GetAsync("/__test__/boom");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("Error interno", problem.GetProperty("title").GetString());
        Assert.False(problem.TryGetProperty("stackTrace", out _));
        Assert.DoesNotContain("InvalidOperationException", problem.GetRawText(), StringComparison.Ordinal);

        var traceId = problem.GetProperty("traceId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(traceId));
    }

    [Fact]
    public async Task Una_regla_de_negocio_responde_422_con_errors_por_campo()
    {
        var response = await Client().GetAsync("/__test__/business-rule");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var vat = problem.GetProperty("errors").GetProperty("vat");
        Assert.Equal("fuera de la lista permitida", vat[0].GetString());
    }

    [Fact]
    public async Task Un_NotFound_y_un_recurso_de_otro_taller_responden_igual()
    {
        var notFound = await Client().GetAsync("/__test__/not-found");
        var crossTenant = await Client().GetAsync("/__test__/cross-tenant");

        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, crossTenant.StatusCode);
        Assert.Equal(
            await ReadTitleAsync(notFound),
            await ReadTitleAsync(crossTenant));
    }

    [Fact]
    public async Task Un_conflicto_de_unicidad_de_la_base_se_mapea_a_409()
    {
        var response = await Client().GetAsync("/__test__/conflict");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("Conflicto", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Una_violacion_de_concurrencia_responde_412()
    {
        var response = await Client().GetAsync("/__test__/concurrency");

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
    }

    [Fact]
    public async Task El_traceId_de_la_respuesta_coincide_con_el_de_los_logs()
    {
        LogCapture.Clear();
        var response = await Client().GetAsync("/__test__/boom");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        var traceId = problem.GetProperty("traceId").GetString();

        Assert.Contains(LogCapture.Events, e => e.Properties.TryGetValue("TraceId", out var v)
            && v?.ToString()?.Trim('"') == traceId);
    }

    [Fact]
    public async Task El_log_de_la_solicitud_omite_el_token_del_QR_de_la_ruta()
    {
        LogCapture.Clear();
        await Client().GetAsync("/q/this-is-a-very-long-base64url-token-value");

        var request = LogCapture.Events.Single(e => e.MessageTemplate.Text.StartsWith("HTTP ", StringComparison.Ordinal));
        var path = request.Properties["Path"].ToString().Trim('"');
        Assert.Equal("/q/{token}", path);
        Assert.All(LogCapture.Events, e => Assert.DoesNotContain("this-is-a-very-long", e.RenderMessage(), StringComparison.Ordinal));
    }

    private static async Task<string?> ReadTitleAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("title").GetString();
}
