using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TallerControl.Api.IntegrationTests.Auth;
using TallerControl.Api.IntegrationTests.Persistence;
using TallerControl.Application.Auth;
using TallerControl.Application.Common;
using TallerControl.Domain.Catalog;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Users;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Api.IntegrationTests.Catalog;

/// <summary>
/// Factory compartida para los tests de catálogo (T-CAT-*, T-STK-*).
/// Siembra un taller con Owner (todos los permisos, incluido <c>stock.adjust</c>)
/// y un mecánico (solo <c>catalog.read</c>, sin <c>stock.adjust</c>).
/// Cada test crea sus propios ítems para garantizar aislamiento.
/// </summary>
internal sealed class CatalogWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string OwnerEmail = "owner@catalog-test.com";
    public const string OwnerPassword = "OwnerPassword!2026";

    /// <summary>Mecánico: tiene catalog.read pero NO catalog.write ni stock.adjust (T-CAT permisos).</summary>
    public const string MechanicEmail = "mechanic@catalog-test.com";
    public const string MechanicPassword = "MechanicPassword!2026";

    public Guid TenantId { get; private set; }
    public string ConnectionString { get; }

    public CatalogWebApplicationFactory(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public static async Task<CatalogWebApplicationFactory> CreateAsync(PostgresFixture postgres)
    {
        var cs = await postgres.CreateDatabaseAsync();
        var appCs = PostgresFixture.AsAppRole(cs);

        await using (var migrator = new AppDbContext(
            BuildOptions(cs), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            await migrator.Database.MigrateAsync();
        }

        var factory = new CatalogWebApplicationFactory(appCs);
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
        });
    }

    private async Task SeedAsync(string ownerConnectionString)
    {
        await using var scope = Services.CreateAsyncScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var tenant = new Tenant("Taller Catálogo Test", DateTimeOffset.UtcNow);
        var settings = new TenantSettings(tenant.Id);

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            db.Add(tenant);
            db.Add(settings);
            await db.SaveChangesAsync();
        }

        TenantId = tenant.Id;

        var owner = new User(OwnerEmail, "Dueño Test");
        owner.Activate(hasher.Hash(OwnerPassword));

        var mechanic = new User(MechanicEmail, "Mecánico Test");
        mechanic.Activate(hasher.Hash(MechanicPassword));

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            db.Add(owner);
            db.Add(mechanic);
            await db.SaveChangesAsync();
        }

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenant.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(new Membership(tenant.Id, owner.Id, TenantRole.Owner));
            db.Add(new Membership(tenant.Id, mechanic.Id, TenantRole.Mechanic));
            db.Add(Branch.CreateMain(tenant.Id));
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
