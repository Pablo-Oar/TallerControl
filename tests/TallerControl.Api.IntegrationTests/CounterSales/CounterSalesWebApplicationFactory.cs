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
using TallerControl.Domain.Customers;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Users;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Api.IntegrationTests.CounterSales;

/// <summary>
/// Factory para los tests de ventas de mostrador (T-CSAL-01..03).
/// Siembra un taller con Owner, un cliente opcional y un ítem del catálogo con stock.
/// </summary>
internal sealed class CounterSalesWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string OwnerEmail = "owner@countersales-test.com";
    public const string OwnerPassword = "OwnerCounterSalesPassword!2026";

    public const string MechanicEmail = "mechanic@countersales-test.com";
    public const string MechanicPassword = "MechanicCounterSalesPassword!2026";

    public Guid TenantId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid CatalogItemId { get; private set; }
    public string ConnectionString { get; }

    public CounterSalesWebApplicationFactory(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public static async Task<CounterSalesWebApplicationFactory> CreateAsync(PostgresFixture postgres)
    {
        var cs = await postgres.CreateDatabaseAsync();
        var appCs = PostgresFixture.AsAppRole(cs);

        await using (var migrator = new AppDbContext(
            BuildOptions(cs), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            await migrator.Database.MigrateAsync();
        }

        var factory = new CounterSalesWebApplicationFactory(appCs);
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

        var tenant = new Tenant("Taller VentaMostrador Test", DateTimeOffset.UtcNow);
        var settings = new TenantSettings(tenant.Id);

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            db.Add(tenant);
            db.Add(settings);
            await db.SaveChangesAsync();
        }

        TenantId = tenant.Id;

        var owner = new User(OwnerEmail, "Dueño VentaMostrador Test");
        owner.Activate(hasher.Hash(OwnerPassword));

        var mechanic = new User(MechanicEmail, "Mecánico VentaMostrador Test");
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

        // Cliente y ítem de catálogo para las pruebas de ventas de mostrador
        var customer = new Customer(tenant.Id, "Cliente VentaMostrador Test");
        var catalogItem = new CatalogItem(tenant.Id, CatalogItemKind.Product, "Producto T-CSAL",
            sku: null, netPrice: 100m, vatRate: 21, cost: null, trackStock: true, stockMin: null,
            durationMinutes: null, bufferMinutes: null);

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenant.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(customer);
            db.Add(catalogItem);
            await db.SaveChangesAsync();
        }

        CustomerId = customer.Id;
        CatalogItemId = catalogItem.Id;
    }

    private static DbContextOptions<AppDbContext> BuildOptions(string cs)
    {
        var b = new DbContextOptionsBuilder<AppDbContext>();
        b.UseTallerControlNpgsql(cs);
        return b.Options;
    }
}
