using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TallerControl.Api.IntegrationTests.Auth;
using TallerControl.Api.IntegrationTests.Persistence;
using TallerControl.Application.Auth;
using TallerControl.Application.Common;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Users;
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
        ownerA.Activate(hasher.Hash(Password));
        var adminA = new User(AdminAEmail, "Administrativo A");
        adminA.Activate(hasher.Hash(Password));
        var mechanicA = new User(MechanicAEmail, "Mecánico A");
        mechanicA.Activate(hasher.Hash(Password));
        var ownerB = new User(OwnerBEmail, "Dueño B");
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

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenantA.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(new Membership(tenantA.Id, ownerA.Id, TenantRole.Owner));
            db.Add(adminAMembership);
            db.Add(new Membership(tenantA.Id, mechanicA.Id, TenantRole.Mechanic));
            await db.SaveChangesAsync();
        }

        ForeignMembershipId = adminAMembership.Id;

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString), new TestTenantContext(tenantB.Id), new TestActorContext(), TimeProvider.System))
        {
            db.Add(new Membership(tenantB.Id, ownerB.Id, TenantRole.Owner));
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
