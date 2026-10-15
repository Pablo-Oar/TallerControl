using System.Net;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Serilog;
using TallerControl.Api.Endpoints.Auth;
using TallerControl.Api.Endpoints.Catalog;
using TallerControl.Api.Endpoints.Customers;
using TallerControl.Api.Endpoints.Dashboard;
using TallerControl.Api.Endpoints.Payments;
using TallerControl.Api.Endpoints.Public;
using TallerControl.Api.Endpoints.Qr;
using TallerControl.Api.Endpoints.Quotes;
using TallerControl.Api.Endpoints.Registration;
using TallerControl.Api.Endpoints.Settings;
using TallerControl.Api.Endpoints.Users;
using TallerControl.Api.Endpoints.Vehicles;
using TallerControl.Api.Endpoints.Work;
using TallerControl.Api.Errors;
using TallerControl.Api.Identity;
using TallerControl.Api.Logging;
using TallerControl.Api.Security;
using TallerControl.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// preserveStaticLogger: las pruebas levantan varios hosts en el mismo proceso.
builder.Host.UseSerilog(
    (context, _, logger) => SerilogConfiguration.Configure(context, logger),
    preserveStaticLogger: true);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    options.KnownProxies.Add(IPAddress.Loopback);
    options.KnownProxies.Add(IPAddress.IPv6Loopback);
});

builder.Services.AddInfrastructure(builder.Configuration);

// Data Protection: claves persistidas en BD para sobrevivir reinicios y escala horizontal (E9).
// Contexto separado para evitar dep transitiva vulnerable en Infrastructure (solo Web SDK la absorbe).
builder.Services.AddDbContext<DataProtectionDbContext>((sp, options) =>
    options.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>()));
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<DataProtectionDbContext>();

builder.Services.AddTallerControlAuthentication();
builder.Services.AddTallerControlRateLimiting();
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

app.UseForwardedHeaders();
app.UseSecurityHeaders();
app.UseRateLimiter();
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
app.MapUsersEndpoints();
app.MapSettingsEndpoints();
app.MapCustomersEndpoints();
app.MapVehiclesEndpoints();
app.MapCatalogEndpoints();
app.MapWorkOrdersEndpoints();
app.MapAttachmentsEndpoints();
app.MapQuotesEndpoints();
app.MapPaymentsEndpoints();
app.MapQrCardsEndpoints();
app.MapDashboardEndpoints();
app.MapRegistrationEndpoints(app.Configuration);
app.MapPublicEndpoints();

app.Run();

// Necesario para WebApplicationFactory<Program> en las pruebas de integración.
public partial class Program;
