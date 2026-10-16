using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TallerControl.Api.IntegrationTests.Auth;
using TallerControl.Api.IntegrationTests.Persistence;
using TallerControl.Application.Auth;
using TallerControl.Application.Common;
using TallerControl.Application.Invoicing;
using TallerControl.Domain.Customers;
using TallerControl.Domain.Invoicing;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Users;
using TallerControl.Domain.Vehicles;
using TallerControl.Domain.Work;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Api.IntegrationTests.Invoicing;

/// <summary>
/// Factory para los tests de facturación electrónica (T-CERT-01..03, T-INV-01..07).
/// Siembra: taller con Owner y AdminStaff; un certificado ARCA activo; un punto de venta;
/// un cliente (ConsumidorFinal) con vehículo y una OT completada lista para facturar.
/// Sustituye IArcaClient con FakeArcaClient para evitar llamadas reales a ARCA.
/// </summary>
internal sealed class InvoicingWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string OwnerEmail = "owner@invoicing-test.com";
    public const string AdminEmail = "admin@invoicing-test.com";
    public const string Password = "InvoicingTestPassword!2026";

    public Guid TenantId { get; private set; }
    public Guid CertificateId { get; private set; }
    public Guid PointOfSaleId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid WorkOrderId { get; private set; }

    public string ConnectionString { get; }

    public InvoicingWebApplicationFactory(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public static async Task<InvoicingWebApplicationFactory> CreateAsync(PostgresFixture postgres)
    {
        var cs = await postgres.CreateDatabaseAsync();
        var appCs = PostgresFixture.AsAppRole(cs);

        await using (var migrator = new AppDbContext(
            BuildOptions(cs), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            await migrator.Database.MigrateAsync();
        }

        var factory = new InvoicingWebApplicationFactory(appCs);
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

            // Sustituir IArcaClient con el stub; nunca llama a ARCA real.
            services.RemoveAll<IArcaClient>();
            services.AddSingleton<IArcaClient>(new FakeArcaClient());

            // Sustituir el encryptor; los bytes [1,2,3] sembrados no son un payload
            // válido de Data Protection API, así que usamos el stub que los acepta tal cual.
            services.RemoveAll<IFiscalCertificateEncryptor>();
            services.AddSingleton<IFiscalCertificateEncryptor>(new FakeFiscalCertificateEncryptor());
        });
    }

    private async Task SeedAsync(string ownerConnectionString)
    {
        await using var scope = Services.CreateAsyncScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        // ── Taller ────────────────────────────────────────────────────────────
        var tenant = new Tenant("Taller Facturación Test", DateTimeOffset.UtcNow);
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
        var owner = new User(OwnerEmail, "Dueño Facturación Test");
        var admin = new User(AdminEmail, "Administrativo Facturación Test");

        owner.Activate(hasher.Hash(Password));
        admin.Activate(hasher.Hash(Password));

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            db.Add(owner);
            db.Add(admin);
            await db.SaveChangesAsync();
        }

        // ── Membresías y sucursal ─────────────────────────────────────────────
        var branch = Branch.CreateMain(tenant.Id);

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenant.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(new Membership(tenant.Id, owner.Id, TenantRole.Owner));
            db.Add(new Membership(tenant.Id, admin.Id, TenantRole.AdminStaff));
            db.Add(branch);
            await db.SaveChangesAsync();
        }

        // ── Certificado ARCA activo ───────────────────────────────────────────
        // La clave privada cifrada es un placeholder (los tests usan FakeArcaClient).
        var cert = new FiscalCertificate(
            tenant.Id, "20123456789", "CUIT Test", isPrimary: true,
            privateKeyEncrypted: [1, 2, 3],
            csrPem: "-----BEGIN CERTIFICATE REQUEST-----\nFAKECSR\n-----END CERTIFICATE REQUEST-----");
        cert.MarkUploaded(
            "-----BEGIN CERTIFICATE-----\nFAKECERT\n-----END CERTIFICATE-----",
            DateTimeOffset.UtcNow.AddYears(1));

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenant.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(cert);
            await db.SaveChangesAsync();
        }

        CertificateId = cert.Id;

        // ── Punto de venta ────────────────────────────────────────────────────
        var pos = new FiscalPointOfSale(tenant.Id, cert.Id, 1, "Punto de Venta Test");

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenant.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(pos);
            await db.SaveChangesAsync();
        }

        PointOfSaleId = pos.Id;

        // ── Cliente y vehículo ────────────────────────────────────────────────
        var customer = new Customer(tenant.Id, "Cliente Factura Test");
        var vehicle = new Vehicle(tenant.Id, customer.Id, "INVTST1");

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenant.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(customer);
            db.Add(vehicle);
            await db.SaveChangesAsync();
        }

        CustomerId = customer.Id;
        VehicleId = vehicle.Id;

        // ── Orden de trabajo con un ítem de servicio para emitir factura ─────
        // La OT debe tener montos > 0; el Invoice constructor rechaza netAmount <= 0.
        var workOrder = new WorkOrder(
            tenant.Id, branch.Id, number: 1,
            customer.Id, vehicle.Id,
            odometerKm: 0, fuelLevel: null, reportedIssue: "Test de facturación",
            WorkOrderPriority.Normal, assignedMechanicId: null, sourceQuoteId: null,
            createdBy: null, createdAt: DateTimeOffset.UtcNow);

        workOrder.AddItem(
            WorkOrderItemKind.Service,
            description: "Servicio de prueba facturación",
            catalogItemId: null,
            quantity: 1m,
            unitNetPrice: 1000m,
            vatRate: 21m,
            unitCostSnapshot: null);

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenant.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(workOrder);
            await db.SaveChangesAsync();
        }

        WorkOrderId = workOrder.Id;
    }

    private static DbContextOptions<AppDbContext> BuildOptions(string cs)
    {
        var b = new DbContextOptionsBuilder<AppDbContext>();
        b.UseTallerControlNpgsql(cs);
        return b.Options;
    }
}
