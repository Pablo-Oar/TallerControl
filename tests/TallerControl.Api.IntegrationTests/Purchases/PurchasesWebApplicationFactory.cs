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
using TallerControl.Domain.Suppliers;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Users;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Api.IntegrationTests.Purchases;

/// <summary>
/// Factory para los tests de compras (T-PUR-01..05).
/// Siembra un taller con Owner, un proveedor y un ítem de catálogo para las pruebas de ítems.
/// </summary>
internal sealed class PurchasesWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string OwnerEmail = "owner@purchases-test.com";
    public const string OwnerPassword = "OwnerPurchasesPassword!2026";

    public const string MechanicEmail = "mechanic@purchases-test.com";
    public const string MechanicPassword = "MechanicPurchasesPassword!2026";

    public Guid TenantId { get; private set; }
    public Guid SupplierId { get; private set; }
    public Guid CatalogItemId { get; private set; }
    public string ConnectionString { get; }

    public PurchasesWebApplicationFactory(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public static async Task<PurchasesWebApplicationFactory> CreateAsync(PostgresFixture postgres)
    {
        var cs = await postgres.CreateDatabaseAsync();
        var appCs = PostgresFixture.AsAppRole(cs);

        await using (var migrator = new AppDbContext(
            BuildOptions(cs), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            await migrator.Database.MigrateAsync();
        }

        var factory = new PurchasesWebApplicationFactory(appCs);
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

        var tenant = new Tenant("Taller Compras Test", DateTimeOffset.UtcNow);
        var settings = new TenantSettings(tenant.Id);

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            db.Add(tenant);
            db.Add(settings);
            await db.SaveChangesAsync();
        }

        TenantId = tenant.Id;

        var owner = new User(OwnerEmail, "Dueño Compras Test");
        owner.Activate(hasher.Hash(OwnerPassword));

        var mechanic = new User(MechanicEmail, "Mecánico Compras Test");
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

        // Proveedor para las pruebas de compras
        var supplier = new Supplier(tenant.Id, "Proveedor Compras Test",
            cuit: null, VatCondition.ResponsableInscripto, address: null, phone: null, email: null, notes: null);

        // Ítem del catálogo para los ítems de compra
        var catalogItem = new CatalogItem(tenant.Id, CatalogItemKind.Product, "Repuesto T-PUR",
            sku: null, netPrice: 500m, vatRate: 21, cost: null, trackStock: true, stockMin: null,
            durationMinutes: null, bufferMinutes: null);

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenant.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(supplier);
            db.Add(catalogItem);
            await db.SaveChangesAsync();
        }

        SupplierId = supplier.Id;
        CatalogItemId = catalogItem.Id;
    }

    private static DbContextOptions<AppDbContext> BuildOptions(string cs)
    {
        var b = new DbContextOptionsBuilder<AppDbContext>();
        b.UseTallerControlNpgsql(cs);
        return b.Options;
    }
}
