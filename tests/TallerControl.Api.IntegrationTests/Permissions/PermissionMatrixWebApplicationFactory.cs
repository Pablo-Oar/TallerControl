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

namespace TallerControl.Api.IntegrationTests.Permissions;

/// <summary>
/// Factory para T-PERM-ALL y T-ISO-ALL (doc 08). Siembra el Taller A con los tres roles base
/// (para recorrer la matriz de doc 07 §3) y el Taller B con un dueño (para probar que sus
/// permisos no le sirven para ver recursos del Taller A, doc 08 T-ISO-ALL).
/// </summary>
internal sealed class PermissionMatrixWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string OwnerAEmail = "owner@perm-taller-a.com";
    public const string AdminAEmail = "admin@perm-taller-a.com";
    public const string MechanicAEmail = "mechanic@perm-taller-a.com";
    public const string OwnerBEmail = "owner@perm-taller-b.com";
    public const string Password = "PermMatrixPassword!2026";

    public Guid TenantAId { get; private set; }
    public Guid TenantBId { get; private set; }

    /// <summary>Recurso del Taller A (la membresía del administrativo) para probar T-ISO-ALL.</summary>
    public Guid ForeignMembershipId { get; private set; }

    /// <summary>Cliente del Taller A sembrado para probar T-ISO-ALL en rutas /customers/{id}.</summary>
    public Guid ForeignCustomerId { get; private set; }

    /// <summary>Vehículo del Taller A sembrado para probar T-ISO-ALL en rutas /vehicles/{id}.</summary>
    public Guid ForeignVehicleId { get; private set; }

    /// <summary>Ítem del catálogo del Taller A sembrado para probar T-ISO-ALL en rutas /catalog/{id}.</summary>
    public Guid ForeignCatalogItemId { get; private set; }

    /// <summary>OT del Taller A sembrada para probar T-ISO-ALL en rutas /work-orders/{id}.</summary>
    public Guid ForeignWorkOrderId { get; private set; }

    /// <summary>Adjunto del Taller A sembrado para probar T-ISO-ALL en rutas /attachments/{id}.</summary>
    public Guid ForeignAttachmentId { get; private set; }

    public string ConnectionString { get; }

    public PermissionMatrixWebApplicationFactory(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public static async Task<PermissionMatrixWebApplicationFactory> CreateAsync(PostgresFixture postgres)
    {
        var cs = await postgres.CreateDatabaseAsync();
        var appCs = PostgresFixture.AsAppRole(cs);

        await using (var migrator = new AppDbContext(
            BuildOptions(cs), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            await migrator.Database.MigrateAsync();
        }

        var factory = new PermissionMatrixWebApplicationFactory(appCs);
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

        var tenantA = new Tenant("Taller Matriz A", DateTimeOffset.UtcNow);
        var tenantB = new Tenant("Taller Matriz B", DateTimeOffset.UtcNow);

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            db.Add(tenantA);
            db.Add(tenantB);
            db.Add(new TenantSettings(tenantA.Id));
            db.Add(new TenantSettings(tenantB.Id));
            await db.SaveChangesAsync();
        }

        TenantAId = tenantA.Id;
        TenantBId = tenantB.Id;

        var ownerA = new User(OwnerAEmail, "Dueño A");
        var adminA = new User(AdminAEmail, "Administrativo A");
        var mechanicA = new User(MechanicAEmail, "Mecánico A");
        var ownerB = new User(OwnerBEmail, "Dueño B");

        ownerA.Activate(hasher.Hash(Password));
        adminA.Activate(hasher.Hash(Password));
        mechanicA.Activate(hasher.Hash(Password));
        ownerB.Activate(hasher.Hash(Password));

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            db.Add(ownerA);
            db.Add(adminA);
            db.Add(mechanicA);
            db.Add(ownerB);
            await db.SaveChangesAsync();
        }

        var adminAMembership = new Membership(tenantA.Id, adminA.Id, TenantRole.AdminStaff);

        // La sucursal principal del Taller A se siembra junto con las membresías para que esté
        // disponible antes de crear la OT (que requiere branch_id).
        var branchA = Branch.CreateMain(tenantA.Id);

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenantA.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(new Membership(tenantA.Id, ownerA.Id, TenantRole.Owner));
            db.Add(adminAMembership);
            db.Add(new Membership(tenantA.Id, mechanicA.Id, TenantRole.Mechanic));
            db.Add(branchA);
            await db.SaveChangesAsync();
        }

        ForeignMembershipId = adminAMembership.Id;

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenantB.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(new Membership(tenantB.Id, ownerB.Id, TenantRole.Owner));
            await db.SaveChangesAsync();
        }

        // Siembra un Cliente del Taller A para T-ISO-ALL en rutas /customers/{id}
        var customerA = new Customer(tenantA.Id, "Cliente T-ISO-ALL");
        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenantA.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(customerA);
            await db.SaveChangesAsync();
        }

        ForeignCustomerId = customerA.Id;

        // Siembra un Vehículo del Taller A para T-ISO-ALL en rutas /vehicles/{id}
        var vehicleA = new Vehicle(tenantA.Id, customerA.Id, "ISOTST");
        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenantA.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(vehicleA);
            await db.SaveChangesAsync();
        }

        ForeignVehicleId = vehicleA.Id;

        // Siembra un ítem del catálogo del Taller A para T-ISO-ALL en rutas /catalog/{id}
        var catalogItemA = new CatalogItem(tenantA.Id, CatalogItemKind.Service, "Servicio T-ISO-ALL",
            sku: null, netPrice: 0, vatRate: 21, cost: null, trackStock: false, stockMin: null,
            durationMinutes: null, bufferMinutes: null);
        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenantA.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(catalogItemA);
            await db.SaveChangesAsync();
        }

        ForeignCatalogItemId = catalogItemA.Id;

        // Siembra una OT del Taller A para T-ISO-ALL en rutas /work-orders/{id}.
        // Número 1 directo: en tests de aislamiento no importa la secuencia.
        var workOrderA = new WorkOrder(
            tenantA.Id, branchA.Id, number: 1,
            customerA.Id, vehicleA.Id,
            odometerKm: 0, fuelLevel: null, reportedIssue: null,
            WorkOrderPriority.Normal, assignedMechanicId: null, sourceQuoteId: null,
            createdBy: null, createdAt: DateTimeOffset.UtcNow);

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenantA.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(workOrderA);
            await db.SaveChangesAsync();
        }

        ForeignWorkOrderId = workOrderA.Id;

        // Siembra un adjunto pendiente del Taller A para T-ISO-ALL en rutas /attachments/{id}.
        var attachmentA = new Attachment(
            tenantA.Id, AttachmentOwnerType.WorkOrder, workOrderA.Id,
            fileKey: "tenants/iso-test/att.jpg",
            contentType: "image/jpeg",
            sizeBytes: 1024,
            isInternal: false,
            createdBy: null,
            createdAt: DateTimeOffset.UtcNow);

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenantA.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(attachmentA);
            await db.SaveChangesAsync();
        }

        ForeignAttachmentId = attachmentA.Id;
    }

    private static DbContextOptions<AppDbContext> BuildOptions(string cs)
    {
        var b = new DbContextOptionsBuilder<AppDbContext>();
        b.UseTallerControlNpgsql(cs);
        return b.Options;
    }
}
