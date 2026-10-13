using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TallerControl.Api.IntegrationTests.Auth;
using TallerControl.Api.IntegrationTests.Persistence;
using TallerControl.Application.Auth;
using TallerControl.Application.Common;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Users;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Api.IntegrationTests.Settings;

/// <summary>
/// Factory para tests de configuración del taller (T-SET-01..03, REQ-04).
/// Siembra un taller con su <see cref="TenantSettings"/> por defecto, un dueño y un administrativo
/// (sin <c>settings.manage</c>, según doc 07 §3). Reemplaza <c>IObjectStorage</c> por
/// <see cref="FakeObjectStorage"/>: no hay RustFS real en los tests.
/// </summary>
internal sealed class SettingsWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string OwnerEmail = "owner@settings-taller.com";
    public const string OwnerPassword = "OwnerPassword!2026";

    public const string AdminEmail = "admin@settings-taller.com";
    public const string AdminPassword = "AdminPassword!2026";

    public const string TenantName = "Taller Settings Test";

    public Guid TenantId { get; private set; }
    public FakeObjectStorage Storage { get; } = new();
    public string ConnectionString { get; }

    public SettingsWebApplicationFactory(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public static async Task<SettingsWebApplicationFactory> CreateAsync(PostgresFixture postgres)
    {
        var cs = await postgres.CreateDatabaseAsync();
        var appCs = PostgresFixture.AsAppRole(cs);

        await using (var migrator = new AppDbContext(
            BuildOptions(cs), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            await migrator.Database.MigrateAsync();
        }

        var factory = new SettingsWebApplicationFactory(appCs);
        await factory.SeedAsync(cs);
        return factory;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(new FakeEmailSender());

            services.RemoveAll<IObjectStorage>();
            services.AddSingleton<IObjectStorage>(Storage);
        });
    }

    private async Task SeedAsync(string ownerConnectionString)
    {
        await using var scope = Services.CreateAsyncScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var tenant = new Tenant(TenantName, DateTimeOffset.UtcNow);
        var settings = new TenantSettings(tenant.Id);

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            db.Add(tenant);
            db.Add(settings);
            await db.SaveChangesAsync();
        }

        TenantId = tenant.Id;

        var owner = new User(OwnerEmail, "Dueño");
        owner.Activate(hasher.Hash(OwnerPassword));

        var admin = new User(AdminEmail, "Administrativo");
        admin.Activate(hasher.Hash(AdminPassword));

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            db.Add(owner);
            db.Add(admin);
            await db.SaveChangesAsync();
        }

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenant.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(new Membership(tenant.Id, owner.Id, TenantRole.Owner));
            db.Add(new Membership(tenant.Id, admin.Id, TenantRole.AdminStaff));
            await db.SaveChangesAsync();
        }
    }

    private static DbContextOptions<AppDbContext> BuildOptions(string cs)
    {
        var b = new DbContextOptionsBuilder<AppDbContext>();
        b.UseTallerControlNpgsql(cs);
        return b.Options;
    }
}
