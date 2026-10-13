using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using TallerControl.Application.Common;
using TallerControl.Infrastructure.Health;
using TallerControl.Infrastructure.Persistence;
using TallerControl.Infrastructure.Tenancy;

namespace TallerControl.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Etiqueta de los health checks que participan de <c>/health/ready</c>.</summary>
    public const string ReadyTag = "ready";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Falta ConnectionStrings:Default (usar dotnet user-secrets o la variable ConnectionStrings__Default).");

        services.AddNpgsqlDataSource(connectionString);

        // Sin sesión por defecto; E2 registra la implementación basada en la cookie de sesión.
        services.TryAddScoped<ITenantContext, NoTenantContext>();
        services.TryAddScoped<IBranchContext, NoTenantContext>();

        services.AddDbContext<AppDbContext>((provider, options) =>
            options.UseTallerControlNpgsql(provider.GetRequiredService<NpgsqlDataSource>()));

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: [ReadyTag]);

        return services;
    }
}
