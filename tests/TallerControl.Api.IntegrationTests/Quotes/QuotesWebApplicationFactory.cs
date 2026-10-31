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
using TallerControl.Domain.Vehicles;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Api.IntegrationTests.Quotes;

/// <summary>
/// Factory compartida para los tests de presupuestos (T-QUO-*, T-PUB-*, T-DOC-01).
/// Siembra: taller con Owner y AdminStaff; un cliente; un vehículo; un ítem de catálogo.
/// Cada test crea sus propios presupuestos.
/// </summary>
internal sealed class QuotesWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string OwnerEmail = "owner@quotes-test.com";
    public const string AdminEmail = "admin@quotes-test.com";
    public const string Password = "QuotesTestPassword!2026";

    public Guid TenantId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid CatalogItemId { get; private set; }
    public Guid BranchId { get; private set; }

    public string ConnectionString { get; }

    public QuotesWebApplicationFactory(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public static async Task<QuotesWebApplicationFactory> CreateAsync(PostgresFixture postgres)
    {
        var cs = await postgres.CreateDatabaseAsync();
        var appCs = PostgresFixture.AsAppRole(cs);

        await using (var migrator = new AppDbContext(
            BuildOptions(cs), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            await migrator.Database.MigrateAsync();
        }

        var factory = new QuotesWebApplicationFactory(appCs);
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

        var tenant = new Tenant("Taller Presupuestos Test", DateTimeOffset.UtcNow);
        var settings = new TenantSettings(tenant.Id);

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            db.Add(tenant);
            db.Add(settings);
            await db.SaveChangesAsync();
        }

        TenantId = tenant.Id;

        var owner = new User(OwnerEmail, "Dueño Quotes Test");
        var admin = new User(AdminEmail, "Administrativo Quotes");

        owner.Activate(hasher.Hash(Password));
        admin.Activate(hasher.Hash(Password));

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            db.Add(owner);
            db.Add(admin);
            await db.SaveChangesAsync();
        }

        var ownerMembership = new Membership(tenant.Id, owner.Id, TenantRole.Owner);
        var adminMembership = new Membership(tenant.Id, admin.Id, TenantRole.AdminStaff);
        var branch = Branch.CreateMain(tenant.Id);

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenant.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(ownerMembership);
            db.Add(adminMembership);
            db.Add(branch);
            await db.SaveChangesAsync();
        }

        BranchId = branch.Id;

        var customer = new Customer(tenant.Id, "Cliente Quotes Test", email: "cliente@quotes-test.com");
        var vehicle = new Vehicle(tenant.Id, customer.Id, "QTEST01");

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenant.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(customer);
            db.Add(vehicle);
            await db.SaveChangesAsync();
        }

        CustomerId = customer.Id;
        VehicleId = vehicle.Id;

        var catalogItem = new CatalogItem(
            tenant.Id, CatalogItemKind.Service, "Servicio Quotes Test",
            sku: null, netPrice: 1000m, vatRate: 21, cost: null,
            trackStock: false, stockMin: null, durationMinutes: null, bufferMinutes: null);

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenant.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(catalogItem);
            await db.SaveChangesAsync();
        }

        CatalogItemId = catalogItem.Id;
    }

    private static DbContextOptions<AppDbContext> BuildOptions(string cs)
    {
        var b = new DbContextOptionsBuilder<AppDbContext>();
        b.UseTallerControlNpgsql(cs);
        return b.Options;
    }
}
