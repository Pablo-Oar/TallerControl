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

namespace TallerControl.Api.IntegrationTests.Users;

/// <summary>
/// Factory para tests de gestión de usuarios (T-PERM-01, T-USR-05..07).
/// Siembra dos dueños: <see cref="Owner1Email"/> y <see cref="Owner2Email"/>, y un administrativo.
/// </summary>
internal sealed class UsersWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string Owner1Email = "owner1@users-taller.com";
    public const string Owner1Password = "Owner1Password!2026";
    public const string Owner1DisplayName = "Dueño Uno";

    public const string Owner2Email = "owner2@users-taller.com";
    public const string Owner2Password = "Owner2Password!2026";
    public const string Owner2DisplayName = "Dueño Dos";

    public const string AdminEmail = "admin@users-taller.com";
    public const string AdminPassword = "AdminPassword!2026";
    public const string AdminDisplayName = "Administrativo";

    public const string TenantName = "Taller Users Test";

    public Guid Owner1UserId { get; private set; }
    public Guid Owner1MembershipId { get; private set; }
    public Guid Owner2UserId { get; private set; }
    public Guid Owner2MembershipId { get; private set; }
    public Guid AdminMembershipId { get; private set; }
    public Guid TenantId { get; private set; }
    public string ConnectionString { get; }

    public UsersWebApplicationFactory(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public static async Task<UsersWebApplicationFactory> CreateAsync(PostgresFixture postgres)
    {
        var cs = await postgres.CreateDatabaseAsync();
        var appCs = PostgresFixture.AsAppRole(cs);

        await using (var migrator = new AppDbContext(
            BuildOptions(cs),
            new TestTenantContext(),
            new TestActorContext(),
            TimeProvider.System))
        {
            await migrator.Database.MigrateAsync();
        }

        var factory = new UsersWebApplicationFactory(appCs);
        await factory.SeedUsersAsync(cs);
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

    private async Task SeedUsersAsync(string ownerConnectionString)
    {
        await using var scope = Services.CreateAsyncScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var tenant = new Tenant(TenantName, DateTimeOffset.UtcNow);
        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString),
            new TestTenantContext(),
            new TestActorContext(),
            TimeProvider.System))
        {
            db.Add(tenant);
            await db.SaveChangesAsync();
        }

        TenantId = tenant.Id;

        var owner1 = new User(Owner1Email, Owner1DisplayName);
        owner1.Activate(hasher.Hash(Owner1Password));

        var owner2 = new User(Owner2Email, Owner2DisplayName);
        owner2.Activate(hasher.Hash(Owner2Password));

        var admin = new User(AdminEmail, AdminDisplayName);
        admin.Activate(hasher.Hash(AdminPassword));

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString),
            new TestTenantContext(),
            new TestActorContext(),
            TimeProvider.System))
        {
            db.Add(owner1);
            db.Add(owner2);
            db.Add(admin);
            await db.SaveChangesAsync();
        }

        var m1 = new Membership(tenant.Id, owner1.Id, TenantRole.Owner);
        var m2 = new Membership(tenant.Id, owner2.Id, TenantRole.Owner);
        var mAdmin = new Membership(tenant.Id, admin.Id, TenantRole.AdminStaff);

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString),
            new TestTenantContext(tenant.Id),
            new TestActorContext(),
            TimeProvider.System))
        {
            db.Add(m1);
            db.Add(m2);
            db.Add(mAdmin);
            await db.SaveChangesAsync();
        }

        Owner1UserId = owner1.Id;
        Owner1MembershipId = m1.Id;
        Owner2UserId = owner2.Id;
        Owner2MembershipId = m2.Id;
        AdminMembershipId = mAdmin.Id;
    }

    private static DbContextOptions<AppDbContext> BuildOptions(string cs)
    {
        var b = new DbContextOptionsBuilder<AppDbContext>();
        b.UseTallerControlNpgsql(cs);
        return b.Options;
    }
}
