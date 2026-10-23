using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TallerControl.Api.IntegrationTests.Auth;
using TallerControl.Api.IntegrationTests.Persistence;
using TallerControl.Application.Auth;
using TallerControl.Application.Common;
using TallerControl.Domain.Cash;
using TallerControl.Domain.Catalog;
using TallerControl.Domain.CounterSales;
using TallerControl.Domain.Customers;
using TallerControl.Domain.Invoicing;
using TallerControl.Domain.Payments;
using TallerControl.Domain.Purchases;
using TallerControl.Domain.Qr;
using TallerControl.Domain.Quotes;
using TallerControl.Domain.Scheduling;
using TallerControl.Domain.Suppliers;
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

    /// <summary>Presupuesto del Taller A sembrado para probar T-ISO-ALL en rutas /quotes/{id}.</summary>
    public Guid ForeignQuoteId { get; private set; }

    /// <summary>Pago del Taller A sembrado para probar T-ISO-ALL en rutas /work-orders/{id}/payments/{id}.</summary>
    public Guid ForeignPaymentId { get; private set; }

    /// <summary>Tarjeta QR del Taller A sembrada para probar T-ISO-ALL en rutas /qr/cards/{id}.</summary>
    public Guid ForeignQrCardId { get; private set; }

    /// <summary>Cuenta de caja del Taller A para probar T-ISO-ALL en rutas /cash/accounts/{id}.</summary>
    public Guid ForeignCashAccountId { get; private set; }

    /// <summary>Proveedor del Taller A para probar T-ISO-ALL en rutas /suppliers/{id}.</summary>
    public Guid ForeignSupplierId { get; private set; }

    /// <summary>Compra del Taller A para probar T-ISO-ALL en rutas /purchases/{id}.</summary>
    public Guid ForeignPurchaseId { get; private set; }

    /// <summary>Venta de mostrador del Taller A para probar T-ISO-ALL en rutas /counter-sales/{id}.</summary>
    public Guid ForeignCounterSaleId { get; private set; }

    /// <summary>Factura del Taller A para probar T-ISO-ALL en rutas /invoicing/invoices/{id}.</summary>
    public Guid ForeignInvoiceId { get; private set; }

    /// <summary>Certificado fiscal del Taller A para probar T-ISO-ALL en rutas /invoicing/certificates/{id}.</summary>
    public Guid ForeignCertificateId { get; private set; }

    /// <summary>Punto de venta del Taller A para probar T-ISO-ALL en rutas /invoicing/points-of-sale/{id}.</summary>
    public Guid ForeignPointOfSaleId { get; private set; }

    /// <summary>Recordatorio del Taller A para probar T-ISO-ALL en rutas /reminders/{id}.</summary>
    public Guid ForeignReminderId { get; private set; }

    /// <summary>Aviso del Taller A para probar T-ISO-ALL en rutas /notifications/{id}.</summary>
    public Guid ForeignNotificationId { get; private set; }
    public Guid ForeignInvitationId { get; private set; }

    /// <summary>Turno del Taller A para probar T-ISO-ALL en rutas /appointments/{id}.</summary>
    public Guid ForeignAppointmentId { get; private set; }

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

        // Siembra un presupuesto del Taller A para T-ISO-ALL en rutas /quotes/{id}.
        var quoteA = new Quote(
            tenantA.Id, number: 1,
            customerA.Id, vehicleA.Id,
            validUntil: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            customerNotes: null, internalNotes: null, createdBy: null);

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenantA.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(quoteA);
            await db.SaveChangesAsync();
        }

        ForeignQuoteId = quoteA.Id;

        // Siembra un pago del Taller A para T-ISO-ALL en rutas /work-orders/{id}/payments/{id}.
        var fakeHash = new byte[32];
        var paymentA = new Payment(
            tenantA.Id, workOrderA.Id, number: 1,
            PaymentMethod.Cash, amount: 100m,
            paidAt: DateTimeOffset.UtcNow,
            reference: null, publicTokenHash: fakeHash,
            idempotencyKey: null, createdBy: null,
            createdAt: DateTimeOffset.UtcNow);

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenantA.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(paymentA);
            await db.SaveChangesAsync();
        }

        ForeignPaymentId = paymentA.Id;

        // Siembra una tarjeta QR del Taller A para T-ISO-ALL en rutas /qr/cards/{id}.
        var qrCardA = new QrCard(tenantA.Id, QrCodeGenerator.GenerateCode(), Guid.NewGuid(), DateTimeOffset.UtcNow);

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenantA.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(qrCardA);
            await db.SaveChangesAsync();
        }

        ForeignQrCardId = qrCardA.Id;

        // Siembra una cuenta de caja del Taller A para T-ISO-ALL en rutas /cash/accounts/{id}.
        var cashAccountA = new CashAccount(tenantA.Id, branchA.Id, "Caja T-ISO-ALL", CashAccountKind.Cash);
        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenantA.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(cashAccountA);
            await db.SaveChangesAsync();
        }

        ForeignCashAccountId = cashAccountA.Id;

        // Siembra un proveedor del Taller A para T-ISO-ALL en rutas /suppliers/{id}.
        var supplierA = new Supplier(tenantA.Id, "Proveedor T-ISO-ALL",
            cuit: null, VatCondition.ConsumidorFinal, address: null, phone: null, email: null, notes: null);
        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenantA.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(supplierA);
            await db.SaveChangesAsync();
        }

        ForeignSupplierId = supplierA.Id;

        // Siembra una compra del Taller A para T-ISO-ALL en rutas /purchases/{id}.
        var purchaseA = new Purchase(
            tenantA.Id, branchA.Id, supplierA.Id,
            DateTimeOffset.UtcNow, pointOfSale: null, invoiceNumber: null,
            perceptions: 0, retentions: 0, documentNumber: 1);
        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenantA.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(purchaseA);
            await db.SaveChangesAsync();
        }

        ForeignPurchaseId = purchaseA.Id;

        // Siembra una venta de mostrador del Taller A para T-ISO-ALL en rutas /counter-sales/{id}.
        var counterSaleA = new CounterSale(tenantA.Id, branchA.Id, customerId: null, documentNumber: 1, DateTimeOffset.UtcNow);
        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenantA.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(counterSaleA);
            await db.SaveChangesAsync();
        }

        ForeignCounterSaleId = counterSaleA.Id;

        // Siembra un certificado y una factura del Taller A para T-ISO-ALL en rutas /invoicing/invoices/{id}.
        var certA = new FiscalCertificate(
            tenantA.Id, "20000000001", "Cert T-ISO-ALL", isPrimary: true,
            privateKeyEncrypted: [1, 2, 3], csrPem: "-----BEGIN CERTIFICATE REQUEST-----\nFAKE\n-----END CERTIFICATE REQUEST-----");
        certA.MarkUploaded("-----BEGIN CERTIFICATE-----\nFAKE\n-----END CERTIFICATE-----", DateTimeOffset.UtcNow.AddYears(1));

        var posA = new FiscalPointOfSale(tenantA.Id, certA.Id, 1, "Punto de Venta T-ISO-ALL");

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenantA.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(certA);
            db.Add(posA);
            await db.SaveChangesAsync();
        }

        var invoiceA = new Invoice(
            tenantA.Id, posA.Id,
            InvoiceSourceType.WorkOrder, workOrderA.Id,
            InvoiceType.B,
            netAmount: 100m, vatAmount: 21m, totalAmount: 121m,
            issuedAt: DateTimeOffset.UtcNow);
        invoiceA.Authorize(1, "70417054367466", DateTimeOffset.UtcNow.AddDays(5), "<FakeResponse/>");

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenantA.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(invoiceA);
            await db.SaveChangesAsync();
        }

        ForeignInvoiceId = invoiceA.Id;
        ForeignCertificateId = certA.Id;
        ForeignPointOfSaleId = posA.Id;

        // Siembra un recordatorio del Taller A para T-ISO-ALL en rutas /reminders/{id}.
        var reminderA = new TallerControl.Domain.Reminders.Reminder(
            tenantA.Id, vehicleA.Id,
            TallerControl.Domain.Reminders.ReminderKind.ServiceDue,
            "Service T-ISO-ALL",
            dueDate: DateTimeOffset.UtcNow.AddDays(30),
            dueKm: null, notes: null);
        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenantA.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(reminderA);
            await db.SaveChangesAsync();
        }

        ForeignReminderId = reminderA.Id;

        // Siembra un aviso del Taller A para T-ISO-ALL en rutas /notifications/{id}.
        var notificationA = new TallerControl.Domain.Notifications.Notification(
            tenantA.Id, "test", "Aviso T-ISO-ALL", DateTimeOffset.UtcNow);
        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenantA.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(notificationA);
            await db.SaveChangesAsync();
        }

        ForeignNotificationId = notificationA.Id;

        // Siembra una invitación pendiente del Taller A para T-ISO-ALL en rutas /invitations/{id}.
        var (invitationA, _) = TallerControl.Domain.Users.Invitation.Create(
            tenantA.Id, "foreign-invite@perm-taller-a.com", TallerControl.Domain.Users.TenantRole.Mechanic,
            null, DateTimeOffset.UtcNow, "Invitada A");
        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenantA.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(invitationA);
            await db.SaveChangesAsync();
        }

        ForeignInvitationId = invitationA.Id;

        // Siembra un turno del Taller A para T-ISO-ALL en rutas /appointments/{id}.
        var fakeTokenHash = new byte[32];
        fakeTokenHash[0] = 0xAB;
        var appointmentA = new Appointment(
            tenantA.Id, customerA.Id, vehicleA.Id,
            mechanicMembershipId: null,
            scheduledAt: DateTimeOffset.UtcNow.AddDays(7),
            notes: null,
            confirmationTokenHash: fakeTokenHash);

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenantA.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(appointmentA);
            await db.SaveChangesAsync();
        }

        ForeignAppointmentId = appointmentA.Id;
    }

    private static DbContextOptions<AppDbContext> BuildOptions(string cs)
    {
        var b = new DbContextOptionsBuilder<AppDbContext>();
        b.UseTallerControlNpgsql(cs);
        return b.Options;
    }
}
