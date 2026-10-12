using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace TallerControl.Api.IntegrationTests;

/// <summary>Prueba de humo de E0: el host arranca y expone los health checks.</summary>
public sealed class HealthEndpointsTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private WebApplicationFactory<Program> CreateFactory(string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Default", connectionString));

    [Fact]
    public async Task Live_responde_200_sin_depender_de_la_base()
    {
        // Cadena inalcanzable a propósito: liveness no debe tocar la base.
        await using var factory = CreateFactory("Host=127.0.0.1;Port=1;Database=x;Username=x;Timeout=1");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_responde_200_con_la_base_disponible()
    {
        await using var factory = CreateFactory(_postgres.GetConnectionString());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_responde_503_si_la_base_no_responde()
    {
        await using var factory = CreateFactory("Host=127.0.0.1;Port=1;Database=x;Username=x;Timeout=1");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
