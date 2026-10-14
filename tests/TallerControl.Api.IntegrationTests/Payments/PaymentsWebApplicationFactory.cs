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
using TallerControl.Domain.Work;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Api.IntegrationTests.Payments;

/// <summary>
/// Factory compartida para los tests de pagos (T-PAY-*, T-DOC-03, T-PUB-07).
/// Siembra: taller con Owner y AdminStaff; un cliente; un vehículo; un ítem de catálogo (servicio);
/// una OT Pending con total conocido. Cada test crea sus propios pagos.
/// </summary>
internal sealed class PaymentsWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string OwnerEmail = "owner@payments-test.com";
    public const string AdminEmail = "admin@payments-test.com";
    public const string Password = "PaymentsTestPassword!2026";

    public Guid TenantId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid WorkOrderId { get; private set; }

    /// <summary>Total de la OT sembrada: 1210 (1000 net + 21% IVA).</summary>
    public decimal WorkOrderTotal { get; private set; }

    public string ConnectionString { get; }

    public PaymentsWebApplicationFactory(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public static async Task<PaymentsWebApplicationFactory> CreateAsync(PostgresFixture postgres)
    {
        var cs = await postgres.CreateDatabaseAsync();
        var appCs = PostgresFixture.AsAppRole(cs);

        await using (var migrator = new AppDbContext(
            BuildOptions(cs), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            await migrator.Database.MigrateAsync();
        }

        var factory = new PaymentsWebApplicationFactory(appCs);
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

        var tenant = new Tenant("Taller Pagos Test", DateTimeOffset.UtcNow);
        var settings = new TenantSettings(tenant.Id);

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            db.Add(tenant);
            db.Add(settings);
            await db.SaveChangesAsync();
        }

        TenantId = tenant.Id;

        var owner = new User(OwnerEmail, "Dueño Pagos Test");
        var admin = new User(AdminEmail, "Administrativo Pagos");

        owner.Activate(hasher.Hash(Password));
        admin.Activate(hasher.Hash(Password));

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            db.Add(owner);
            db.Add(admin);
            await db.SaveChangesAsync();
        }

        var branch = Branch.CreateMain(tenant.Id);

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenant.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(new Membership(tenant.Id, owner.Id, TenantRole.Owner));
            db.Add(new Membership(tenant.Id, admin.Id, TenantRole.AdminStaff));
            db.Add(branch);
            await db.SaveChangesAsync();
        }

        var customer = new Customer(tenant.Id, "Cliente Pagos Test");
        var vehicle = new Vehicle(tenant.Id, customer.Id, "PAGTEST");

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenant.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(customer);
            db.Add(vehicle);
            await db.SaveChangesAsync();
        }

        CustomerId = customer.Id;
        VehicleId = vehicle.Id;

        // Servicio de $1000 neto + 21% IVA = $1210 total
        var catalogItem = new CatalogItem(
            tenant.Id, CatalogItemKind.Service, "Servicio Pagos Test",
            sku: null, netPrice: 1000m, vatRate: 21, cost: null,
            trackStock: false, stockMin: null, durationMinutes: null, bufferMinutes: null);

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenant.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(catalogItem);
            await db.SaveChangesAsync();
        }

        // OT con un renglón de servicio → total = 1000 * 1 * 1.21 = $1210.
        var workOrder = new WorkOrder(
            tenant.Id, branch.Id, number: 1,
            customer.Id, vehicle.Id,
            odometerKm: 10000, fuelLevel: null, reportedIssue: "Test",
            WorkOrderPriority.Normal, assignedMechanicId: null, sourceQuoteId: null,
            createdBy: null, createdAt: DateTimeOffset.UtcNow);

        workOrder.AddItem(
            WorkOrderItemKind.Service,
            description: "Servicio Pagos Test",
            catalogItemId: null,
            quantity: 1m,
            unitNetPrice: 1000m,
            vatRate: 21,
            unitCostSnapshot: null);

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenant.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(workOrder);
            await db.SaveChangesAsync();
        }

        WorkOrderId = workOrder.Id;
        WorkOrderTotal = workOrder.Total;
    }

    private static DbContextOptions<AppDbContext> BuildOptions(string cs)
    {
        var b = new DbContextOptionsBuilder<AppDbContext>();
        b.UseTallerControlNpgsql(cs);
        return b.Options;
    }
}
