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
/// Factory para tests de invitaciones y password reset. Expone el <see cref="FakeEmailSender"/>
/// para que los tests puedan verificar emails enviados.
/// </summary>
internal sealed class InvitationWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string SeedEmail = "owner@inv-taller.com";
    public const string SeedPassword = "OwnerInvPassword!2026";
    public const string SeedDisplayName = "Dueño Inv";
    public const string SeedTenantName = "Taller Inv";

    public Guid SeedUserId { get; private set; }
    public Guid SeedTenantId { get; private set; }
    public string ConnectionString { get; }
    public FakeEmailSender EmailSender { get; } = new();

    public InvitationWebApplicationFactory(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public static async Task<InvitationWebApplicationFactory> CreateAsync(PostgresFixture postgres)
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

        var factory = new InvitationWebApplicationFactory(appCs);
        await factory.SeedOwnerAsync(cs);
        return factory;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);

        builder.ConfigureServices(services =>
        {
            // Reemplazar IEmailSender con el fake para capturar emails.
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);
        });
    }

    private async Task SeedOwnerAsync(string ownerConnectionString)
    {
        await using var scope = Services.CreateAsyncScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var tenant = new Tenant(SeedTenantName, DateTimeOffset.UtcNow);
        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString),
            new TestTenantContext(),
            new TestActorContext(),
            TimeProvider.System))
        {
            db.Add(tenant);
            await db.SaveChangesAsync();
        }

        var user = new User(SeedEmail, SeedDisplayName);
        user.Activate(hasher.Hash(SeedPassword));

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString),
            new TestTenantContext(),
            new TestActorContext(),
            TimeProvider.System))
        {
            db.Add(user);
            await db.SaveChangesAsync();
        }

        await using (var db = new AppDbContext(
            BuildOptions(ownerConnectionString),
            new TestTenantContext(tenant.Id),
            new TestActorContext(),
            TimeProvider.System))
        {
            db.Add(new Membership(tenant.Id, user.Id, TenantRole.Owner));
            await db.SaveChangesAsync();
        }

        SeedUserId = user.Id;
        SeedTenantId = tenant.Id;
    }

    private static DbContextOptions<AppDbContext> BuildOptions(string cs)
    {
        var b = new DbContextOptionsBuilder<AppDbContext>();
        b.UseTallerControlNpgsql(cs);
        return b.Options;
    }
}
