using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TallerControl.Infrastructure.Health;

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

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: [ReadyTag]);

        return services;
    }
}
