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

namespace TallerControl.Api.IntegrationTests.Scheduling;

/// <summary>
/// Factory para los tests de agenda y turnos (T-APT-01..04, E14).
/// Siembra un taller con Owner, Mechanic, un cliente, un vehículo, un ítem de catálogo
/// y una regla de disponibilidad para el mecánico.
/// </summary>
internal sealed class AppointmentsWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string OwnerEmail = "owner@appointments-test.com";
    public const string MechanicEmail = "mechanic@appointments-test.com";
    public const string Password = "AppointmentsPassword!2026";

    public Guid TenantId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid CatalogItemId { get; private set; }
    public Guid MechanicMembershipId { get; private set; }
    public string ConnectionString { get; }

    public AppointmentsWebApplicationFactory(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public static async Task<AppointmentsWebApplicationFactory> CreateAsync(PostgresFixture postgres)
    {
        var cs = await postgres.CreateDatabaseAsync();
        var appCs = PostgresFixture.AsAppRole(cs);

        await using (var migrator = new AppDbContext(
            BuildOptions(cs), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            await migrator.Database.MigrateAsync();
        }

        var factory = new AppointmentsWebApplicationFactory(appCs);
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

        var tenant = new Tenant("Taller Agenda Test", DateTimeOffset.UtcNow);
        var settings = new TenantSettings(tenant.Id);

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            db.Add(tenant);
            db.Add(settings);
            await db.SaveChangesAsync();
        }

        TenantId = tenant.Id;

        var owner = new User(OwnerEmail, "Dueño Agenda");
        var mechanic = new User(MechanicEmail, "Mecánico Agenda");

        owner.Activate(hasher.Hash(Password));
        mechanic.Activate(hasher.Hash(Password));

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            db.Add(owner);
            db.Add(mechanic);
            await db.SaveChangesAsync();
        }

        var mechanicMembership = new Membership(tenant.Id, mechanic.Id, TenantRole.Mechanic);
        var customer = new Customer(tenant.Id, "Cliente Agenda");
        var vehicle = new Vehicle(tenant.Id, customer.Id, "APT001");
        var catalogItem = new CatalogItem(
            tenant.Id, CatalogItemKind.Service, "Servicio Prueba Agenda",
            sku: null, netPrice: 100, vatRate: 21, cost: null, trackStock: false, stockMin: null,
            durationMinutes: 45, bufferMinutes: 15);

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenant.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(new Membership(tenant.Id, owner.Id, TenantRole.Owner));
            db.Add(mechanicMembership);
            db.Add(Branch.CreateMain(tenant.Id));
            db.Add(customer);
            db.Add(vehicle);
            db.Add(catalogItem);
            await db.SaveChangesAsync();
        }

        MechanicMembershipId = mechanicMembership.Id;
        CustomerId = customer.Id;
        VehicleId = vehicle.Id;
        CatalogItemId = catalogItem.Id;
    }

    private static DbContextOptions<AppDbContext> BuildOptions(string cs)
    {
        var b = new DbContextOptionsBuilder<AppDbContext>();
        b.UseTallerControlNpgsql(cs);
        return b.Options;
    }
}
