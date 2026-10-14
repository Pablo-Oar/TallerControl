using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TallerControl.Api.IntegrationTests.Auth;
using TallerControl.Api.IntegrationTests.Persistence;
using TallerControl.Application.Auth;
using TallerControl.Application.Common;
using TallerControl.Application.Qr;
using TallerControl.Domain.Customers;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Users;
using TallerControl.Domain.Vehicles;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Api.IntegrationTests.Qr;

/// <summary>
/// Factory compartida para T-QR-02..23 y T-DOC-04.
/// Siembra: taller Owner + AdminStaff + Mechanic; una sucursal; un cliente con código de acceso;
/// un vehículo. Cada test crea sus propias tarjetas QR.
/// </summary>
internal sealed class QrWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string OwnerEmail = "owner@qr-test.com";
    public const string AdminEmail = "admin@qr-test.com";
    public const string MechanicEmail = "mechanic@qr-test.com";
    public const string Password = "QrTestPassword!2026";

    public Guid TenantId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid BranchId { get; private set; }

    public string ConnectionString { get; }

    public QrWebApplicationFactory(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public static async Task<QrWebApplicationFactory> CreateAsync(PostgresFixture postgres)
    {
        var cs = await postgres.CreateDatabaseAsync();
        var appCs = PostgresFixture.AsAppRole(cs);

        await using (var migrator = new AppDbContext(
            BuildOptions(cs), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            await migrator.Database.MigrateAsync();
        }

        var factory = new QrWebApplicationFactory(appCs);
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

            // Reemplazar con stub para evitar llamadas reales a Cloudflare en tests
            services.RemoveAll<ITurnstileVerifier>();
            services.AddSingleton<ITurnstileVerifier>(new AlwaysPassTurnstileVerifier());
        });
    }

    private async Task SeedAsync(string ownerConnectionString)
    {
        await using var scope = Services.CreateAsyncScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var tenant = new Tenant("Taller QR Test", DateTimeOffset.UtcNow);
        var settings = new TenantSettings(tenant.Id);

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            db.Add(tenant);
            db.Add(settings);
            await db.SaveChangesAsync();
        }

        TenantId = tenant.Id;

        var owner = new User(OwnerEmail, "Dueño QR Test");
        var admin = new User(AdminEmail, "Administrativo QR");
        var mechanic = new User(MechanicEmail, "Mecánico QR");

        owner.Activate(hasher.Hash(Password));
        admin.Activate(hasher.Hash(Password));
        mechanic.Activate(hasher.Hash(Password));

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(), new TestActorContext(), TimeProvider.System))
        {
            db.Add(owner);
            db.Add(admin);
            db.Add(mechanic);
            await db.SaveChangesAsync();
        }

        var branch = Branch.CreateMain(tenant.Id);

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenant.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(new Membership(tenant.Id, owner.Id, TenantRole.Owner));
            db.Add(new Membership(tenant.Id, admin.Id, TenantRole.AdminStaff));
            db.Add(new Membership(tenant.Id, mechanic.Id, TenantRole.Mechanic));
            db.Add(branch);
            await db.SaveChangesAsync();
        }

        BranchId = branch.Id;

        // Cliente con email para el flujo OTP y con código de acceso generado
        var customer = new Customer(tenant.Id, "Cliente QR Test", email: "cliente@qr-test.com");
        customer.GenerateOwnerAccessCode();

        var vehicle = new Vehicle(tenant.Id, customer.Id, "QRTEST");

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenant.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(customer);
            db.Add(vehicle);
            await db.SaveChangesAsync();
        }

        CustomerId = customer.Id;
        VehicleId = vehicle.Id;
    }

    private static DbContextOptions<AppDbContext> BuildOptions(string cs)
    {
        var b = new DbContextOptionsBuilder<AppDbContext>();
        b.UseTallerControlNpgsql(cs);
        return b.Options;
    }
}

internal sealed class AlwaysPassTurnstileVerifier : ITurnstileVerifier
{
    public Task<bool> VerifyAsync(string token, string? clientIp, CancellationToken ct) =>
        Task.FromResult(true);
}
