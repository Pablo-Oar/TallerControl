using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using TallerControl.Api.Errors;
using TallerControl.Api.Logging;
using TallerControl.Application.Common;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Api.IntegrationTests.Errors;

/// <summary>
/// Host mínimo en memoria con el mismo pipeline de errores y logs que <c>Program</c>
/// (<see cref="ErrorHandlingExtensions.UseApiErrorPipeline"/>) y un endpoint por tipo de excepción.
/// </summary>
internal static class TestHost
{
    public static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production,
        });
        builder.WebHost.UseTestServer();
        builder.Host.UseSerilog(
            (_, _, logger) => logger
                // Mismos niveles que appsettings.json: el log de hosting del framework (que trae la
                // ruta sin sanear) queda en Warning y no se emite.
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.With<TraceEnricher>()
                .WriteTo.Sink(LogCapture.Sink),
            preserveStaticLogger: true);
        builder.Services.AddApiErrorHandling();

        var app = builder.Build();
        app.UseApiErrorPipeline();

        // Tipo de retorno explícito: sin él, el lambda que solo lanza se confunde con RequestDelegate.
        app.MapGet("/__test__/boom", IResult () =>
            throw new InvalidOperationException("algo salió muy mal"));

        app.MapGet("/__test__/business-rule", IResult () =>
            throw new BusinessRuleException(
                "IVA no permitido",
                new Dictionary<string, string[]> { ["vat"] = ["fuera de la lista permitida"] }));

        app.MapGet("/__test__/not-found", IResult () =>
            throw new NotFoundException());

        app.MapGet("/__test__/cross-tenant", IResult () =>
            throw new TenantViolationException("Branch pertenece a otro taller."));

        app.MapGet("/__test__/conflict", IResult () =>
        {
            var postgres = PostgresExceptionFactory.Create(Npgsql.PostgresErrorCodes.UniqueViolation);
            throw new DbUpdateException("duplicado", postgres);
        });

        app.MapGet("/__test__/concurrency", IResult () =>
            throw new DbUpdateConcurrencyException("otro usuario modificó el recurso"));

        await app.StartAsync();
        return app;
    }
}
