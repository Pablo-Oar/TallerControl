using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TallerControl.Api.IntegrationTests.Auth;
using TallerControl.Api.IntegrationTests.Persistence;
using TallerControl.Application.Auth;
using TallerControl.Application.Common;
using TallerControl.Domain.Customers;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Users;
using TallerControl.Domain.Vehicles;
using TallerControl.Domain.Work;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Api.IntegrationTests.Dashboard;

/// <summary>
/// Factory para los tests del dashboard (T-DASH-01, T-DASH-02).
/// Siembra: un taller con Owner; un cliente y vehículo; dos OTs abiertas con distintos saldos;
/// una OT cancelada con saldo que NO debe sumarse al totalReceivable.
/// </summary>
internal sealed class DashboardWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string OwnerEmail = "owner@dashboard-test.com";
    public const string Password = "DashboardTestPassword!2026";

    public Guid TenantId { get; private set; }

    /// <summary>Saldo esperado: suma de balances de las dos OTs abiertas (cancelada excluida).</summary>
    public decimal ExpectedTotalReceivable { get; private set; }

    public string ConnectionString { get; }

    public DashboardWebApplicationFactory(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public static async Task<DashboardWebApplicationFactory> CreateAsync(PostgresFixture postgres)
    {
        var cs = await postgres.CreateDatabaseAsync();
        var appCs = PostgresFixture.AsAppRole(cs);

        await using (var migrator = new AppDbContext(
            BuildOptions(cs), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            await migrator.Database.MigrateAsync();
        }

        var factory = new DashboardWebApplicationFactory(appCs);
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

    private async Task SeedAsync(string ownerConnectionString)
    {
        await using var scope = Services.CreateAsyncScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var tenant = new Tenant("Taller Dashboard Test", DateTimeOffset.UtcNow);
        var settings = new TenantSettings(tenant.Id);
        var branch = Branch.CreateMain(tenant.Id);

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            db.Add(tenant);
            db.Add(settings);
            await db.SaveChangesAsync();
        }

        TenantId = tenant.Id;

        var owner = new User(OwnerEmail, "Dueño Dashboard Test");
        owner.Activate(hasher.Hash(Password));

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            db.Add(owner);
            await db.SaveChangesAsync();
        }

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenant.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(new Membership(tenant.Id, owner.Id, TenantRole.Owner));
            db.Add(branch);
            await db.SaveChangesAsync();
        }

        var customer = new Customer(tenant.Id, "Cliente Dashboard Test");
        var vehicle = new Vehicle(tenant.Id, customer.Id, "DASHTEST");

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenant.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(customer);
            db.Add(vehicle);
            await db.SaveChangesAsync();
        }

        // OT 1: $1000 neto + 21% IVA = $1210 total, sin pagos → balance $1210
        var wo1 = new WorkOrder(
            tenant.Id, branch.Id, number: 1,
            customer.Id, vehicle.Id,
            odometerKm: 10000, fuelLevel: null, reportedIssue: "OT 1",
            WorkOrderPriority.Normal, assignedMechanicId: null, sourceQuoteId: null,
            createdBy: null, createdAt: DateTimeOffset.UtcNow);
        wo1.AddItem(WorkOrderItemKind.Service, "Servicio 1", null, 1m, 1000m, 21, null);

        // OT 2: $500 neto + 0% IVA = $500 total, sin pagos → balance $500
        var wo2 = new WorkOrder(
            tenant.Id, branch.Id, number: 2,
            customer.Id, vehicle.Id,
            odometerKm: 10001, fuelLevel: null, reportedIssue: "OT 2",
            WorkOrderPriority.Normal, assignedMechanicId: null, sourceQuoteId: null,
            createdBy: null, createdAt: DateTimeOffset.UtcNow);
        wo2.AddItem(WorkOrderItemKind.Service, "Servicio 2", null, 1m, 500m, 0, null);

        // OT 3: $2000 neto, se cancela → balance debe ser 0 (no cuenta en totalReceivable)
        var wo3 = new WorkOrder(
            tenant.Id, branch.Id, number: 3,
            customer.Id, vehicle.Id,
            odometerKm: 10002, fuelLevel: null, reportedIssue: "OT 3 (cancelada)",
            WorkOrderPriority.Normal, assignedMechanicId: null, sourceQuoteId: null,
            createdBy: null, createdAt: DateTimeOffset.UtcNow);
        wo3.AddItem(WorkOrderItemKind.Service, "Servicio cancelado", null, 1m, 2000m, 21, null);
        wo3.CambiarEstado(
            WorkOrderStatus.Cancelled,
            isMechanic: false, canDeliver: false, canCancel: true,
            cancelReason: "Prueba T-DASH-01",
            note: null, changedBy: null, changedAt: DateTimeOffset.UtcNow);

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenant.Id), new TestActorContext(), TimeProvider.System))
        {
            db.AddRange(wo1, wo2, wo3);
            await db.SaveChangesAsync();
        }

        // balance = 1210 (wo1) + 500 (wo2), wo3 cancelada aporta 0
        ExpectedTotalReceivable = wo1.Total + wo2.Total;
    }

    private static DbContextOptions<AppDbContext> BuildOptions(string cs)
    {
        var b = new DbContextOptionsBuilder<AppDbContext>();
        b.UseTallerControlNpgsql(cs);
        return b.Options;
    }
}
