using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using TallerControl.Api.Errors;
using TallerControl.Api.Logging;
using TallerControl.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// preserveStaticLogger: las pruebas levantan varios hosts en el mismo proceso.
builder.Host.UseSerilog(
    (context, _, logger) => SerilogConfiguration.Configure(context, logger),
    preserveStaticLogger: true);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiErrorHandling();

var app = builder.Build();

app.UseApiErrorPipeline();

// Liveness: el proceso responde. No consulta dependencias.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

// Readiness: dependencias listas (hoy PostgreSQL; R2 se suma con adjuntos, E5).
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(DependencyInjection.ReadyTag),
});

app.Run();

// Necesario para WebApplicationFactory<Program> en las pruebas de integración.
public partial class Program;
