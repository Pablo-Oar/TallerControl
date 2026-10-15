using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TallerControl.Api.IntegrationTests.Auth;
using TallerControl.Api.IntegrationTests.Persistence;
using TallerControl.Application.Common;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Api.IntegrationTests.Registration;

/// <summary>
/// Factory para tests de auto-registro (E10). Base de datos migrada y vacía —
/// no hay ningún taller sembrado; los tests los crean llamando al endpoint.
/// Expone <see cref="EmailSender"/> para verificar correos encolados.
/// </summary>
internal sealed class RegistrationWebApplicationFactory : WebApplicationFactory<Program>
{
    public string ConnectionString { get; }
    public FakeEmailSender EmailSender { get; } = new();

    public RegistrationWebApplicationFactory(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public static async Task<RegistrationWebApplicationFactory> CreateAsync(PostgresFixture postgres)
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

        return new RegistrationWebApplicationFactory(appCs);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);
        });

    }

    private static DbContextOptions<AppDbContext> BuildOptions(string cs)
    {
        var b = new DbContextOptionsBuilder<AppDbContext>();
        b.UseTallerControlNpgsql(cs);
        return b.Options;
    }
}
