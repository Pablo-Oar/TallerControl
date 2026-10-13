using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Auth;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Users;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Api.Identity;

/// <summary>
/// Crea el dueño inicial y su taller en entorno <c>Development</c> para poder probar login y
/// <c>/me</c> antes de que exista el flujo de invitaciones (bloque 3 de E2). Idempotente: si el
/// email ya existe no toca nada. Las credenciales viajan por configuración (<c>dotnet user-secrets</c>
/// o variables de entorno con prefijo <c>Seed__Owner__*</c>). En <c>Production</c>/<c>Staging</c>
/// este hosted service no se registra.
/// </summary>
internal sealed class DevelopmentSeeder : IHostedService
{
    public const string ConfigSection = "Seed:Owner";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _clock;
    private readonly ILogger<DevelopmentSeeder> _logger;

    public DevelopmentSeeder(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        TimeProvider clock,
        ILogger<DevelopmentSeeder> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _clock = clock;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var section = _configuration.GetSection(ConfigSection);
        var email = section["Email"];
        var password = section["Password"];
        var tenantName = section["TenantName"];
        var displayName = section["DisplayName"] ?? "Dueño";

        if (string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(password)
            || string.IsNullOrWhiteSpace(tenantName))
        {
            _logger.LogInformation(
                "Development seed deshabilitado: configurar {Section}:Email, {Section}:Password y {Section}:TenantName vía user-secrets.",
                ConfigSection,
                ConfigSection,
                ConfigSection);
            return;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var existing = await db.Set<User>()
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
        if (existing is not null)
        {
            _logger.LogInformation("Development seed: el usuario inicial ya existe.");
            return;
        }

        var tenant = new Tenant(tenantName, _clock.GetUtcNow());
        db.Add(tenant);
        db.Add(new TenantSettings(tenant.Id));

        var user = new User(normalizedEmail, displayName);
        user.Activate(hasher.Hash(password));
        db.Add(user);

        var membership = new Membership(tenant.Id, user.Id, TenantRole.Owner);
        db.Add(membership);

        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Development seed: usuario inicial {Email} y taller {Tenant} creados.",
            normalizedEmail,
            tenant.Id);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
