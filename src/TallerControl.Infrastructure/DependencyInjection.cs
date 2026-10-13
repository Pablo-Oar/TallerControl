using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using TallerControl.Application.Auth;
using TallerControl.Application.Common;
using TallerControl.Application.Tenancy;
using TallerControl.Application.Users;
using TallerControl.Infrastructure.Email;
using TallerControl.Infrastructure.Health;
using TallerControl.Infrastructure.Identity;
using TallerControl.Infrastructure.Outbox;
using TallerControl.Infrastructure.Persistence;
using TallerControl.Infrastructure.Tenancy;
using TallerControl.Infrastructure.Users;

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
        services.TryAddScoped<IActorContext, SystemActorContext>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IOutbox, OutboxWriter>();

        services.AddDbContext<AppDbContext>((provider, options) =>
            options.UseTallerControlNpgsql(provider.GetRequiredService<NpgsqlDataSource>()));

        services.AddScoped<IDocumentNumberGenerator, DocumentNumberGenerator>();

        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddScoped<IAuthRepository, EfAuthRepository>();
        services.AddScoped<SecurityStampValidator>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<MeHandler>();

        services.AddScoped<IInvitationRepository, EfInvitationRepository>();
        services.AddScoped<IPasswordResetRepository, EfPasswordResetRepository>();
        services.AddScoped<CreateInvitationHandler>();
        services.AddScoped<AcceptInvitationHandler>();
        services.AddScoped<ForgotPasswordHandler>();
        services.AddScoped<ResetPasswordHandler>();

        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddHostedService<OutboxDispatcher>();

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: [ReadyTag]);

        return services;
    }
}
