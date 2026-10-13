using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using TallerControl.Api.Endpoints.Auth;
using TallerControl.Api.Endpoints.Users;
using TallerControl.Api.Errors;
using TallerControl.Api.Identity;
using TallerControl.Api.Logging;
using TallerControl.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// preserveStaticLogger: las pruebas levantan varios hosts en el mismo proceso.
builder.Host.UseSerilog(
    (context, _, logger) => SerilogConfiguration.Configure(context, logger),
    preserveStaticLogger: true);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddTallerControlAuthentication();
builder.Services.AddApiErrorHandling();

// Enums como strings en snake_case para que el frontend reciba valores legibles y estables.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(
        namingPolicy: System.Text.Json.JsonNamingPolicy.SnakeCaseLower));
});

// En desarrollo creamos un dueño inicial para poder probar login y /me antes del flujo de invitaciones.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHostedService<DevelopmentSeeder>();
}

var app = builder.Build();

app.UseApiErrorPipeline();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Liveness: el proceso responde. No consulta dependencias.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

// Readiness: dependencias listas (hoy PostgreSQL; R2 se suma con adjuntos, E5).
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(DependencyInjection.ReadyTag),
});

app.MapAuthEndpoints(app.Configuration);
app.MapInvitationEndpoints(app.Configuration);

app.Run();

// Necesario para WebApplicationFactory<Program> en las pruebas de integración.
public partial class Program;
