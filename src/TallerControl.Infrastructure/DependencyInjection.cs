using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using TallerControl.Application.Auth;
using TallerControl.Application.Catalog;
using TallerControl.Application.Common;
using TallerControl.Application.Customers;
using TallerControl.Application.Payments;
using TallerControl.Application.PublicLinks;
using TallerControl.Application.Quotes;
using TallerControl.Application.Settings;
using TallerControl.Application.Tenancy;
using TallerControl.Application.Users;
using TallerControl.Application.Vehicles;
using TallerControl.Application.Work;
using TallerControl.Infrastructure.Catalog;
using TallerControl.Infrastructure.Customers;
using TallerControl.Infrastructure.Email;
using TallerControl.Infrastructure.Health;
using TallerControl.Infrastructure.Identity;
using TallerControl.Infrastructure.Outbox;
using TallerControl.Infrastructure.Payments;
using TallerControl.Infrastructure.Persistence;
using TallerControl.Infrastructure.PublicLinks;
using TallerControl.Infrastructure.Quotes;
using TallerControl.Infrastructure.Settings;
using TallerControl.Infrastructure.Storage;
using TallerControl.Infrastructure.Tenancy;
using TallerControl.Infrastructure.Users;
using TallerControl.Infrastructure.Vehicles;
using TallerControl.Infrastructure.Work;

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
        services.AddScoped<IMembershipRepository, EfMembershipRepository>();
        services.AddScoped<CreateInvitationHandler>();
        services.AddScoped<AcceptInvitationHandler>();
        services.AddScoped<ForgotPasswordHandler>();
        services.AddScoped<ResetPasswordHandler>();
        services.AddScoped<ListMembersHandler>();
        services.AddScoped<UpdateMembershipHandler>();

        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.AddSingleton<IObjectStorage, S3ObjectStorage>();
        services.AddScoped<ISettingsRepository, EfSettingsRepository>();
        services.AddScoped<GetSettingsHandler>();
        services.AddScoped<UpdateSettingsHandler>();
        services.AddScoped<GetLogoUploadUrlHandler>();
        services.AddScoped<ConfirmLogoHandler>();

        services.AddScoped<ICustomerRepository, EfCustomerRepository>();
        services.AddScoped<SearchCustomersHandler>();
        services.AddScoped<CreateCustomerHandler>();
        services.AddScoped<GetCustomerHandler>();
        services.AddScoped<UpdateCustomerHandler>();
        services.AddScoped<DeleteCustomerHandler>();
        services.AddScoped<RestoreCustomerHandler>();

        services.AddScoped<IVehicleRepository, EfVehicleRepository>();
        services.AddScoped<SearchVehiclesHandler>();
        services.AddScoped<CreateVehicleHandler>();
        services.AddScoped<GetVehicleHandler>();
        services.AddScoped<UpdateVehicleHandler>();
        services.AddScoped<DeleteVehicleHandler>();
        services.AddScoped<GetVehicleHistoryHandler>();

        services.AddScoped<ICatalogRepository, EfCatalogRepository>();
        services.AddScoped<SearchCatalogHandler>();
        services.AddScoped<CreateCatalogItemHandler>();
        services.AddScoped<GetCatalogItemHandler>();
        services.AddScoped<UpdateCatalogItemHandler>();
        services.AddScoped<DeleteCatalogItemHandler>();
        services.AddScoped<RestoreCatalogItemHandler>();
        services.AddScoped<ManualStockAdjustmentHandler>();
        services.AddScoped<GetStockMovementsHandler>();

        services.AddScoped<IWorkOrderRepository, EfWorkOrderRepository>();
        services.AddScoped<IAttachmentRepository, EfAttachmentRepository>();
        services.AddScoped<SearchWorkOrdersHandler>();
        services.AddScoped<GetWorkOrderHandler>();
        services.AddScoped<CreateWorkOrderHandler>();
        services.AddScoped<UpdateWorkOrderHandler>();
        services.AddScoped<ChangeWorkOrderStatusHandler>();
        services.AddScoped<AssignMechanicHandler>();
        services.AddScoped<UpdateMechanicNotesHandler>();
        services.AddScoped<GetWorkOrderHistoryHandler>();
        services.AddScoped<GetWorkOrderPdfHandler>();
        services.AddScoped<GetWorkOrderPublicLinkHandler>();
        services.AddScoped<IWorkOrderPdfGenerator, WorkOrderPdfGenerator>();
        services.AddScoped<RequestAttachmentUploadUrlHandler>();
        services.AddScoped<ConfirmAttachmentHandler>();
        services.AddScoped<ListAttachmentsHandler>();
        services.AddScoped<UpdateAttachmentHandler>();
        services.AddScoped<DeleteAttachmentHandler>();

        services.AddScoped<IQuoteRepository, EfQuoteRepository>();
        services.AddScoped<ITenantSettingsReader, EfTenantSettingsReader>();
        services.AddScoped<IQuotePdfGenerator, QuotePdfGenerator>();
        services.AddScoped<CreateQuoteHandler>();
        services.AddScoped<UpdateQuoteHandler>();
        services.AddScoped<GetQuoteHandler>();
        services.AddScoped<SearchQuotesHandler>();
        services.AddScoped<SendQuoteHandler>();
        services.AddScoped<ReopenQuoteHandler>();
        services.AddScoped<ConvertQuoteHandler>();
        services.AddScoped<CancelQuoteHandler>();
        services.AddScoped<DeleteQuoteHandler>();
        services.AddScoped<GetQuoteResponsesHandler>();
        services.AddScoped<GetQuotePdfHandler>();

        services.AddScoped<IPaymentRepository, EfPaymentRepository>();
        services.AddScoped<IPaymentPdfGenerator, PaymentPdfGenerator>();
        services.AddScoped<GetPaymentsHandler>();
        services.AddScoped<CreatePaymentHandler>();
        services.AddScoped<VoidPaymentHandler>();
        services.AddScoped<GetPaymentPdfHandler>();
        services.AddScoped<GetPublicPaymentHandler>();

        services.AddScoped<IPublicLinkRepository, EfPublicLinkRepository>();
        services.AddScoped<GetPublicQuoteHandler>();
        services.AddScoped<SubmitPublicResponseHandler>();
        services.AddScoped<GetPublicWorkOrderHandler>();

        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddHostedService<OutboxDispatcher>();

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: [ReadyTag]);

        return services;
    }
}
