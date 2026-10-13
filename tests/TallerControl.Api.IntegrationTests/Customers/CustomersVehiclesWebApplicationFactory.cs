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

namespace TallerControl.Api.IntegrationTests.Customers;

/// <summary>
/// Factory compartida para los tests de clientes (T-CUS-*) y vehículos (T-VEH-*).
/// Siembra un taller con Owner (todos los permisos, incluido <c>vehicles.odometer.correct</c>)
/// y un AdminStaff (sin <c>vehicles.odometer.correct</c>, para T-VEH-06).
/// Cada test crea sus propios clientes y vehículos para garantizar aislamiento.
/// </summary>
internal sealed class CustomersVehiclesWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string OwnerEmail = "owner@cus-veh-test.com";
    public const string OwnerPassword = "OwnerPassword!2026";

    /// <summary>AdminStaff: tiene vehicles.write pero NO vehicles.odometer.correct (T-VEH-06).</summary>
    public const string AdminEmail = "admin@cus-veh-test.com";
    public const string AdminPassword = "AdminPassword!2026";

    public Guid TenantId { get; private set; }
    public string ConnectionString { get; }

    public CustomersVehiclesWebApplicationFactory(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public static async Task<CustomersVehiclesWebApplicationFactory> CreateAsync(PostgresFixture postgres)
    {
        var cs = await postgres.CreateDatabaseAsync();
        var appCs = PostgresFixture.AsAppRole(cs);

        await using (var migrator = new AppDbContext(
            BuildOptions(cs), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            await migrator.Database.MigrateAsync();
        }

        var factory = new CustomersVehiclesWebApplicationFactory(appCs);
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

        var tenant = new Tenant("Taller Clientes y Vehículos Test", DateTimeOffset.UtcNow);
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

        var admin = new User(AdminEmail, "Administrativo Test");
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
