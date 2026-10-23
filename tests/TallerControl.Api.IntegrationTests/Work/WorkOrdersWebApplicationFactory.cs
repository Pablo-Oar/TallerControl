using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TallerControl.Api.IntegrationTests.Auth;
using TallerControl.Api.IntegrationTests.Persistence;
using TallerControl.Api.IntegrationTests.Push;
using TallerControl.Application.Auth;
using TallerControl.Application.Common;
using TallerControl.Application.Push;
using TallerControl.Domain.Catalog;
using TallerControl.Domain.Customers;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Users;
using TallerControl.Domain.Vehicles;
using TallerControl.Domain.Work;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Api.IntegrationTests.Work;

/// <summary>
/// Factory compartida para los tests de órdenes de trabajo (T-WO-*, T-PERM-05).
/// Siembra: taller con Owner, AdminStaff y Mechanic; un cliente; un vehículo; un ítem de catálogo
/// con seguimiento de stock (10 unidades iniciales). Cada test crea sus propias órdenes.
/// </summary>
internal sealed class WorkOrdersWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string OwnerEmail = "owner@wo-test.com";
    public const string AdminEmail = "admin@wo-test.com";
    public const string MechanicEmail = "mechanic@wo-test.com";
    public const string Password = "WOTestPassword!2026";

    public Guid TenantId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid CatalogItemId { get; private set; }
    public Guid MechanicMembershipId { get; private set; }
    public Guid OtherMechanicMembershipId { get; private set; }
    public Guid MechanicUserId { get; private set; }

    /// <summary>Sustituye al envío push real: registra a quién se le avisó.</summary>
    public RecordingPushSender Push { get; } = new();
    public Guid AdminMembershipId { get; private set; }
    public Guid OwnerMembershipId { get; private set; }

    public string ConnectionString { get; }

    public WorkOrdersWebApplicationFactory(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public static async Task<WorkOrdersWebApplicationFactory> CreateAsync(PostgresFixture postgres)
    {
        var cs = await postgres.CreateDatabaseAsync();
        var appCs = PostgresFixture.AsAppRole(cs);

        await using (var migrator = new AppDbContext(
            BuildOptions(cs), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            await migrator.Database.MigrateAsync();
        }

        var factory = new WorkOrdersWebApplicationFactory(appCs);
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
            services.RemoveAll<IPushSender>();
            services.AddSingleton<IPushSender>(Push);
        });
    }

    private async Task SeedAsync(string ownerConnectionString)
    {
        await using var scope = Services.CreateAsyncScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        // ── Taller ────────────────────────────────────────────────────────────
        var tenant = new Tenant("Taller OT Test", DateTimeOffset.UtcNow);
        var settings = new TenantSettings(tenant.Id);

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            db.Add(tenant);
            db.Add(settings);
            await db.SaveChangesAsync();
        }

        TenantId = tenant.Id;

        // ── Usuarios ──────────────────────────────────────────────────────────
        var owner = new User(OwnerEmail, "Dueño OT Test");
        var admin = new User(AdminEmail, "Administrativo OT");
        var mechanic = new User(MechanicEmail, "Mecánico OT Test");
        var otherMechanic = new User("other-mechanic@wo-test.com", "Otro mecánico OT");

        owner.Activate(hasher.Hash(Password));
        admin.Activate(hasher.Hash(Password));
        mechanic.Activate(hasher.Hash(Password));
        otherMechanic.Activate(hasher.Hash(Password));

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            db.Add(owner);
            db.Add(admin);
            db.Add(mechanic);
            db.Add(otherMechanic);
            await db.SaveChangesAsync();
        }

        // ── Membresías y sucursal ─────────────────────────────────────────────
        var ownerMembership = new Membership(tenant.Id, owner.Id, TenantRole.Owner);
        var adminMembership = new Membership(tenant.Id, admin.Id, TenantRole.AdminStaff);
        var mechanicMembership = new Membership(tenant.Id, mechanic.Id, TenantRole.Mechanic);
        var otherMechanicMembership = new Membership(tenant.Id, otherMechanic.Id, TenantRole.Mechanic);
        var branch = Branch.CreateMain(tenant.Id);

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenant.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(ownerMembership);
            db.Add(adminMembership);
            db.Add(mechanicMembership);
            db.Add(otherMechanicMembership);
            db.Add(branch);
            await db.SaveChangesAsync();
        }

        OwnerMembershipId = ownerMembership.Id;
        AdminMembershipId = adminMembership.Id;
        MechanicMembershipId = mechanicMembership.Id;
        OtherMechanicMembershipId = otherMechanicMembership.Id;
        MechanicUserId = mechanic.Id;

        // ── Cliente y vehículo ────────────────────────────────────────────────
        var customer = new Customer(tenant.Id, "Cliente OT Test");
        var vehicle = new Vehicle(tenant.Id, customer.Id, "WOFACT1");

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenant.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(customer);
            db.Add(vehicle);
            await db.SaveChangesAsync();
        }

        CustomerId = customer.Id;
        VehicleId = vehicle.Id;

        // ── Ítem de catálogo con 10 unidades de stock inicial ─────────────────
        var catalogItem = new CatalogItem(
            tenant.Id, CatalogItemKind.Product, "Filtro W-Test",
            sku: null, netPrice: 500m, vatRate: 21, cost: null,
            trackStock: true, stockMin: 2m, durationMinutes: null, bufferMinutes: null);

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenant.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(catalogItem);
            await db.SaveChangesAsync();
        }

        CatalogItemId = catalogItem.Id;

        // Siembra stock inicial cargando la entidad desde DB, aplicando el movimiento y persistiendo.
        // Se usa ownerConnectionString porque el rol app tiene REVOKE UPDATE en stock_movements.
        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenant.Id), new TestActorContext(), TimeProvider.System))
        {
            var item = await db.Set<CatalogItem>().FindAsync(catalogItem.Id);
            item!.ApplyStockMovement(10m);

            var movement = new StockMovement(
                tenant.Id, branch.Id, item.Id,
                quantity: 10m,
                reason: StockMovementReason.ManualAdjust,
                note: "Stock inicial de prueba",
                workOrderItemId: null,
                createdBy: null,
                createdAt: DateTimeOffset.UtcNow);

            db.Add(movement);
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
