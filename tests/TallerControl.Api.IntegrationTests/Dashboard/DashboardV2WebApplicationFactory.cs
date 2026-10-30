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
using TallerControl.Domain.Customers;
using TallerControl.Domain.Payments;
using TallerControl.Domain.Quotes;
using TallerControl.Domain.Scheduling;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Users;
using TallerControl.Domain.Vehicles;
using TallerControl.Domain.Work;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Api.IntegrationTests.Dashboard;

/// <summary>
/// Factory del Inicio ampliado (T-DASH-03..09). Siembra, para el taller de prueba:
/// OT A pendiente hace 10 días ($1210, trabada con el umbral por defecto de 3 días);
/// OT B pendiente hace 45 días ($500, trabada y con saldo antiguo con el umbral de 30 días);
/// OT C en proceso creada hoy con un cobro parcial de $400 sobre $1000 (con movimiento de caja);
/// OT D completada hace 5 días ($1000, lista para entregar y trabada);
/// dos presupuestos enviados (hace 3 días: caliente; hace 1 hora: no);
/// un turno de hoy sin mecánico, un artículo con stock bajo y una caja con sesión abierta.
/// Más un segundo taller con una OT trabada y un presupuesto caliente que el primero nunca debe ver.
/// </summary>
internal sealed class DashboardV2WebApplicationFactory : WebApplicationFactory<Program>
{
    public const string OwnerEmail = "owner@dashboard-v2-test.com";
    public const string OwnerName = "Dueño Inicio Test";
    public const string Password = "DashboardV2TestPassword!2026";
    public const int ForeignOrderNumber = 99;

    public Guid TenantId { get; private set; }

    public string ConnectionString { get; }

    private string _ownerConnectionString = string.Empty;

    public DashboardV2WebApplicationFactory(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public static async Task<DashboardV2WebApplicationFactory> CreateAsync(PostgresFixture postgres)
    {
        var cs = await postgres.CreateDatabaseAsync();
        var appCs = PostgresFixture.AsAppRole(cs);

        await using (var migrator = new AppDbContext(
            BuildOptions(cs), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            await migrator.Database.MigrateAsync();
        }

        var factory = new DashboardV2WebApplicationFactory(appCs);
        factory._ownerConnectionString = cs;
        await factory.SeedAsync(cs);
        return factory;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);
        builder.UseSetting("Seed:Owner:Email", "");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(new FakeEmailSender());
        });
    }

    /// <summary>Cambia los umbrales del Inicio del taller de prueba (D-42), saltando la API.</summary>
    public async Task SetThresholdsAsync(int hotQuoteHours, int stuckOrderDays, int agedReceivableDays)
    {
        await using var db = NewDb(_ownerConnectionString, TenantId);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE tenant_settings SET hot_quote_hours = {hotQuoteHours}, stuck_order_days = {stuckOrderDays}, aged_receivable_days = {agedReceivableDays} WHERE tenant_id = {TenantId}");
    }

    private async Task SeedAsync(string cs)
    {
        await using var scope = Services.CreateAsyncScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var now = DateTimeOffset.UtcNow;

        var tenant = new Tenant("Taller Inicio Test", now);
        var branch = Branch.CreateMain(tenant.Id);
        var owner = new User(OwnerEmail, OwnerName);
        owner.Activate(hasher.Hash(Password));
        var membership = new Membership(tenant.Id, owner.Id, TenantRole.Owner);

        await using (var db = NewDb(cs, null))
        {
            db.Add(tenant);
            db.Add(new TenantSettings(tenant.Id));
            db.Add(owner);
            await db.SaveChangesAsync();
        }

        TenantId = tenant.Id;

        await using (var db = NewDb(cs, tenant.Id))
        {
            db.Add(membership);
            db.Add(branch);
            await db.SaveChangesAsync();
        }

        var customer = new Customer(tenant.Id, "Cliente Inicio Test");
        var vehicle = new Vehicle(tenant.Id, customer.Id, "DASHV2");

        WorkOrder NewOrder(int number, DateTimeOffset createdAt, decimal netPrice, decimal vat)
        {
            var wo = new WorkOrder(
                tenant.Id, branch.Id, number, customer.Id, vehicle.Id,
                odometerKm: 1000 + number, fuelLevel: null, reportedIssue: $"OT {number}",
                WorkOrderPriority.Normal, assignedMechanicId: null, sourceQuoteId: null,
                createdBy: null, createdAt: createdAt);
            wo.AddItem(WorkOrderItemKind.Service, $"Servicio {number}", null, 1m, netPrice, vat, null);
            return wo;
        }

        var woA = NewOrder(1, now.AddDays(-10), 1000m, 21);   // $1210
        var woB = NewOrder(2, now.AddDays(-45), 500m, 0);     // $500
        var woC = NewOrder(3, now, 1000m, 0);                  // $1000
        var woD = NewOrder(4, now.AddDays(-12), 1000m, 0);     // $1000
        woC.CambiarEstado(WorkOrderStatus.InProgress, false, false, false, null, null, null, now);
        woD.CambiarEstado(WorkOrderStatus.InProgress, false, false, false, null, null, null, now.AddDays(-6));
        woD.CambiarEstado(WorkOrderStatus.Completed, false, false, false, null, null, null, now.AddDays(-5));

        var payment = new Payment(
            tenant.Id, woC.Id, number: 1, PaymentMethod.Cash, 400m, now, null, new byte[32], null, null, now);
        woC.AplicarPago(400m, null, now);

        var account = new CashAccount(tenant.Id, branch.Id, "Caja del mostrador", CashAccountKind.Cash);
        var session = new CashSession(tenant.Id, branch.Id, account.Id, membership.Id, "Apertura de prueba", now.AddHours(-1));
        var movement = new CashMovement(
            tenant.Id, branch.Id, account.Id, 400m, CashMovementDirection.In, CashMovementReason.Payment,
            "payment", payment.Id, null, membership.Id, now);

        Quote NewQuote(int number, DateTimeOffset sentAt)
        {
            var q = new Quote(tenant.Id, number, customer.Id, vehicle.Id, DateOnly.FromDateTime(now.UtcDateTime).AddDays(30), null, null, null);
            q.AddItem(QuoteItemKind.Service, "Servicio presupuestado", null, 1m, 1000m, 21, null);
            q.Send(sentAt);
            return q;
        }

        var hotQuote = NewQuote(1, now.AddDays(-3));
        var freshQuote = NewQuote(2, now.AddHours(-1));

        var appointment = new Appointment(tenant.Id, customer.Id, vehicle.Id, null, now, "Turno de prueba", new byte[32]);

        var lowStock = new CatalogItem(
            tenant.Id, CatalogItemKind.Product, "Filtro de aceite", "FIL-001", 100m, 21m, 50m,
            trackStock: true, stockMin: 5m, durationMinutes: null, bufferMinutes: null);

        await using (var db = NewDb(cs, tenant.Id))
        {
            db.Add(customer);
            db.Add(vehicle);
            db.AddRange(woA, woB, woC, woD);
            db.AddRange(payment, account);
            await db.SaveChangesAsync();
        }

        await using (var db = NewDb(cs, tenant.Id))
        {
            db.AddRange(session, movement, hotQuote, freshQuote, appointment, lowStock);
            await db.SaveChangesAsync();
        }

        // El contexto pisa created_at con la hora real al insertar: se fija la antigüedad de cada orden a mano.
        await using (var db = NewDb(cs, tenant.Id))
        {
            // (el valor en memoria ya fue pisado por el contexto, por eso se repiten las fechas pensadas)
            foreach (var (wo, createdAt) in new[] { (woA, now.AddDays(-10)), (woB, now.AddDays(-45)), (woC, now), (woD, now.AddDays(-12)) })
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE work_orders SET created_at = {createdAt} WHERE id = {wo.Id}");
            }
        }

        await SeedForeignTenantAsync(cs, now);
    }

    /// <summary>Otro taller con una OT trabada y un presupuesto caliente: el Inicio del primero no debe verlos.</summary>
    private static async Task SeedForeignTenantAsync(string cs, DateTimeOffset now)
    {
        var tenant = new Tenant("Otro Taller Test", now);
        var branch = Branch.CreateMain(tenant.Id);

        await using (var db = NewDb(cs, null))
        {
            db.Add(tenant);
            db.Add(new TenantSettings(tenant.Id));
            await db.SaveChangesAsync();
        }

        var customer = new Customer(tenant.Id, "Cliente Ajeno");
        var vehicle = new Vehicle(tenant.Id, customer.Id, "AJENO01");
        var wo = new WorkOrder(
            tenant.Id, branch.Id, ForeignOrderNumber, customer.Id, vehicle.Id,
            odometerKm: 5000, fuelLevel: null, reportedIssue: "OT ajena",
            WorkOrderPriority.Normal, assignedMechanicId: null, sourceQuoteId: null,
            createdBy: null, createdAt: now.AddDays(-100));
        wo.AddItem(WorkOrderItemKind.Service, "Servicio ajeno", null, 1m, 9999m, 0, null);
        var quote = new Quote(tenant.Id, 1, customer.Id, vehicle.Id, DateOnly.FromDateTime(now.UtcDateTime).AddDays(30), null, null, null);
        quote.AddItem(QuoteItemKind.Service, "Servicio ajeno", null, 1m, 9999m, 0, null);
        quote.Send(now.AddDays(-30));

        await using (var db = NewDb(cs, tenant.Id))
        {
            db.Add(branch);
            db.Add(customer);
            db.Add(vehicle);
            await db.SaveChangesAsync();
        }

        await using (var db = NewDb(cs, tenant.Id))
        {
            db.AddRange(wo, quote);
            await db.SaveChangesAsync();
        }
    }

    private static AppDbContext NewDb(string cs, Guid? tenantId) =>
        new(BuildOptions(cs), new TestTenantContext(tenantId), new TestActorContext(), TimeProvider.System);

    private static DbContextOptions<AppDbContext> BuildOptions(string cs)
    {
        var b = new DbContextOptionsBuilder<AppDbContext>();
        b.UseTallerControlNpgsql(cs);
        return b.Options;
    }
}
