using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TallerControl.Api.IntegrationTests.Persistence;
using TallerControl.Application.Auth;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Users;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Api.IntegrationTests.Auth;

/// <summary>
/// Levanta el host de la API apuntando a una base migrada y precargada con un dueño activo. Expone
/// los datos de la semilla para que las pruebas de autenticación puedan hacer login.
/// </summary>
internal sealed class AuthWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string SeedEmail = "owner@taller.com";
    public const string SeedPassword = "OwnerSeedPassword!2026";
    public const string SeedDisplayName = "Dueño";
    public const string SeedTenantName = "Taller de prueba";

    public Guid SeedUserId { get; private set; }

    public Guid SeedTenantId { get; private set; }

    public string ConnectionString { get; }

    public AuthWebApplicationFactory(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public static async Task<AuthWebApplicationFactory> CreateAsync(PostgresFixture postgres)
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

        var factory = new AuthWebApplicationFactory(appCs);
        await factory.SeedOwnerAsync(cs);
        return factory;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" evita que el DevelopmentSeeder arranque y duplique el dueño.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);
    }

    private async Task SeedOwnerAsync(string ownerConnectionString)
    {
        // Semilla con el rol dueño (el seeder de desarrollo no corre en entorno "Testing").
        await using var scope = Services.CreateAsyncScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var tenant = new Tenant(SeedTenantName, DateTimeOffset.UtcNow);
        await using (var platform = new AppDbContext(
            BuildOptions(ownerConnectionString),
            new TestTenantContext(),
            new TestActorContext(),
            TimeProvider.System))
        {
            platform.Add(tenant);
            await platform.SaveChangesAsync();
        }

        var user = new User(SeedEmail, SeedDisplayName);
        user.Activate(hasher.Hash(SeedPassword));

        await using (var platform = new AppDbContext(
            BuildOptions(ownerConnectionString),
            new TestTenantContext(),
            new TestActorContext(),
            TimeProvider.System))
        {
            platform.Add(user);
            await platform.SaveChangesAsync();
        }

        await using (var scoped = new AppDbContext(
            BuildOptions(ownerConnectionString),
            new TestTenantContext(tenant.Id),
            new TestActorContext(),
            TimeProvider.System))
        {
            scoped.Add(new Membership(tenant.Id, user.Id, TenantRole.Owner));
            await scoped.SaveChangesAsync();
        }

        SeedUserId = user.Id;
        SeedTenantId = tenant.Id;
    }

    private static DbContextOptions<AppDbContext> BuildOptions(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        builder.UseTallerControlNpgsql(connectionString);
        return builder.Options;
    }
}
