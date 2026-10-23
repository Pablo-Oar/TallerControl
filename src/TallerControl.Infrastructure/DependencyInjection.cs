using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using TallerControl.Application.Auth;
using TallerControl.Application.Cash;
using TallerControl.Application.Catalog;
using TallerControl.Application.Common;
using TallerControl.Application.CounterSales;
using TallerControl.Application.Customers;
using TallerControl.Application.Dashboard;
using TallerControl.Application.Invoicing;
using TallerControl.Application.Onboarding;
using TallerControl.Application.Payments;
using TallerControl.Application.PublicLinks;
using TallerControl.Application.Purchases;
using TallerControl.Application.Push;
using TallerControl.Application.Qr;
using TallerControl.Application.Quotes;
using TallerControl.Application.Registration;
using TallerControl.Application.Reminders;
using TallerControl.Application.Reports;
using TallerControl.Application.Scheduling;
using TallerControl.Application.Settings;
using TallerControl.Application.Suppliers;
using TallerControl.Application.Tenancy;
using TallerControl.Application.Users;
using TallerControl.Application.Vehicles;
using TallerControl.Application.Work;
using TallerControl.Infrastructure.Cash;
using TallerControl.Infrastructure.Catalog;
using TallerControl.Infrastructure.CounterSales;
using TallerControl.Infrastructure.Customers;
using TallerControl.Infrastructure.Dashboard;
using TallerControl.Infrastructure.Email;
using TallerControl.Infrastructure.Health;
using TallerControl.Infrastructure.Identity;
using TallerControl.Infrastructure.Invoicing;
using TallerControl.Infrastructure.Onboarding;
using TallerControl.Infrastructure.Outbox;
using TallerControl.Infrastructure.Payments;
using TallerControl.Infrastructure.Persistence;
using TallerControl.Infrastructure.PublicLinks;
using TallerControl.Infrastructure.Purchases;
using TallerControl.Infrastructure.Push;
using TallerControl.Infrastructure.Qr;
using TallerControl.Infrastructure.Quotes;
using TallerControl.Infrastructure.Reminders;
using TallerControl.Infrastructure.Reports;
using TallerControl.Infrastructure.Scheduling;
using TallerControl.Infrastructure.Settings;
using TallerControl.Infrastructure.Storage;
using TallerControl.Infrastructure.Suppliers;
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

        services.AddScoped<IRegistrationRepository, EfRegistrationRepository>();
        services.AddScoped<RegisterTenantHandler>();
        services.AddScoped<VerifyEmailHandler>();

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
        services.AddScoped<RestoreVehicleHandler>();
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
        services.AddScoped<ListAssignableMechanicsHandler>();
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
        services.AddScoped<GetQuotePublicLinkHandler>();
        services.AddScoped<ReopenQuoteHandler>();
        services.AddScoped<ConvertQuoteHandler>();
        services.AddScoped<CancelQuoteHandler>();
        services.AddScoped<DeleteQuoteHandler>();
        services.AddScoped<GetQuoteResponsesHandler>();
        services.AddScoped<GetSignerDocumentHandler>();
        services.AddScoped<GetQuotePdfHandler>();

        services.AddScoped<IDashboardRepository, EfDashboardRepository>();
        services.AddScoped<GetDashboardHandler>();

        services.AddScoped<IPaymentRepository, EfPaymentRepository>();
        services.AddScoped<IPaymentPdfGenerator, PaymentPdfGenerator>();
        services.AddScoped<GetPaymentsHandler>();
        services.AddScoped<CreatePaymentHandler>();
        services.AddScoped<VoidPaymentHandler>();
        services.AddScoped<GetPaymentPdfHandler>();
        services.AddScoped<GetPaymentPublicLinkHandler>();
        services.AddScoped<GetPublicPaymentHandler>();

        services.AddScoped<IPublicLinkRepository, EfPublicLinkRepository>();
        services.AddScoped<ISignatureEncryptor, SignatureEncryptor>();
        services.AddScoped<GetPublicQuoteHandler>();
        services.AddScoped<SubmitPublicResponseHandler>();
        services.AddScoped<RequestSignatureUploadUrlHandler>();
        services.AddScoped<GetPublicWorkOrderHandler>();

        services.Configure<QrOptions>(configuration.GetSection(QrOptions.Section));
        services.AddScoped<IQrRepository, EfQrRepository>();
        services.AddScoped<IOwnerVerificationRepository, EfOwnerVerificationRepository>();
        services.AddScoped<IQrPdfGenerator, QrPdfGenerator>();
        services.AddScoped<IQrCookieService, QrCookieService>();
        services.AddSingleton<ITurnstileVerifier>(sp =>
            new TurnstileVerifier(new HttpClient(), sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<QrOptions>>()));
        services.AddScoped<GenerateBatchHandler>();
        services.AddScoped<SearchQrCardsHandler>();
        services.AddScoped<GetQrCardHandler>();
        services.AddScoped<AssignQrCardHandler>();
        services.AddScoped<AssignByCodeHandler>();
        services.AddScoped<UnassignQrCardHandler>();
        services.AddScoped<MarkLostHandler>();
        services.AddScoped<RevokeQrCardHandler>();
        services.AddScoped<GetQrCardPrintHandler>();
        services.AddScoped<GetQrCardEventsHandler>();
        services.AddScoped<GetOwnerAccessCodeHandler>();
        services.AddScoped<ScanQrCodeHandler>();
        services.AddScoped<StartOwnerChallengeHandler>();
        services.AddScoped<ValidateOwnerOtpHandler>();
        services.AddScoped<GetOwnerVehicleHandler>();
        services.AddScoped<GetQrVehicleSummaryHandler>();
        services.AddScoped<ReportFoundKeysHandler>();

        services.AddScoped<ICashRepository, EfCashRepository>();
        services.AddScoped<CreateCashAccountHandler>();
        services.AddScoped<GetCashAccountHandler>();
        services.AddScoped<SearchCashAccountsHandler>();
        services.AddScoped<UpdateCashAccountHandler>();
        services.AddScoped<DeleteCashAccountHandler>();
        services.AddScoped<OpenCashSessionHandler>();
        services.AddScoped<CloseCashSessionHandler>();
        services.AddScoped<ManualCashMovementHandler>();
        services.AddScoped<GetCashMovementsHandler>();

        services.AddScoped<ISupplierRepository, EfSupplierRepository>();
        services.AddScoped<CreateSupplierHandler>();
        services.AddScoped<GetSupplierHandler>();
        services.AddScoped<SearchSuppliersHandler>();
        services.AddScoped<UpdateSupplierHandler>();
        services.AddScoped<DeleteSupplierHandler>();
        services.AddScoped<RestoreSupplierHandler>();

        services.AddScoped<IPurchaseRepository, EfPurchaseRepository>();
        services.AddScoped<CreatePurchaseHandler>();
        services.AddScoped<ConfirmPurchaseHandler>();
        services.AddScoped<CancelPurchaseHandler>();
        services.AddScoped<UpdatePurchaseHandler>();
        services.AddScoped<GetPurchaseHandler>();
        services.AddScoped<SearchPurchasesHandler>();
        services.AddScoped<DeletePurchaseHandler>();

        services.AddScoped<ICounterSaleRepository, EfCounterSaleRepository>();
        services.AddScoped<CreateCounterSaleHandler>();
        services.AddScoped<ConfirmCounterSaleHandler>();
        services.AddScoped<CancelCounterSaleHandler>();
        services.AddScoped<GetCounterSaleHandler>();
        services.AddScoped<SearchCounterSalesHandler>();
        services.AddScoped<DeleteCounterSaleHandler>();

        // ── E12: Facturación ARCA ──────────────────────────────────────────────
        services.Configure<ArcaOptions>(configuration.GetSection(ArcaOptions.SectionName));
        services.AddMemoryCache();
        services.AddHttpClient<ArcaClient>();
        services.AddScoped<IArcaClient>(sp => sp.GetRequiredService<ArcaClient>());
        services.AddScoped<IFiscalCertificateEncryptor, FiscalCertificateEncryptor>();
        services.AddScoped<IInvoicingRepository, EfInvoicingRepository>();
        services.AddScoped<IIvaBookRepository, EfIvaBookRepository>();
        services.AddScoped<CreateFiscalCertificateHandler>();
        services.AddScoped<UploadFiscalCertificateHandler>();
        services.AddScoped<GetPublicAppointmentHandler>();
        services.AddScoped<DeclineAppointmentPublicHandler>();
        services.AddScoped<GetAppointmentLinkHandler>();
        services.AddScoped<GetInvoicePdfHandler>();
        services.AddScoped<IInvoicePdfGenerator, Invoicing.InvoicePdfGenerator>();
        services.AddScoped<RevokeFiscalCertificateHandler>();
        services.AddScoped<RenewFiscalCertificateHandler>();
        services.AddScoped<TestFiscalConnectionHandler>();
        services.AddScoped<ListFiscalCertificatesHandler>();
        services.AddScoped<CreateFiscalPointOfSaleHandler>();
        services.AddScoped<UpdateFiscalPointOfSaleHandler>();
        services.AddScoped<ListFiscalPointsOfSaleHandler>();
        services.AddScoped<EmitInvoiceHandler>();
        services.AddScoped<VoidInvoiceHandler>();
        services.AddScoped<GetInvoiceHandler>();
        services.AddScoped<SearchInvoicesHandler>();
        services.AddScoped<GetIvaBookHandler>();
        services.AddScoped<GetCitiExportHandler>();

        // ── E13: Push subscriptions (PWA) ─────────────────────────────────────
        services.AddScoped<IPushSubscriptionRepository, EfPushSubscriptionRepository>();
        services.Configure<PushOptions>(configuration.GetSection(PushOptions.SectionName));
        services.AddHttpClient<IPushSender, WebPushSender>();
        services.AddScoped<SavePushSubscriptionHandler>();
        services.AddScoped<DeletePushSubscriptionHandler>();

        // ── E13: Onboarding ───────────────────────────────────────────────────
        services.AddScoped<IOnboardingRepository, EfOnboardingRepository>();
        services.AddScoped<GetOnboardingHandler>();

        // ── E13: Reportes ─────────────────────────────────────────────────────
        services.AddScoped<IReportsRepository, EfReportsRepository>();
        services.AddScoped<IReportExporter, ClosedXmlExporter>();
        services.AddScoped<GetRevenueReportHandler>();
        services.AddScoped<GetReceivablesReportHandler>();
        services.AddScoped<GetStockReportHandler>();
        services.AddScoped<GetPurchasesReportHandler>();

        // ── E13: Recordatorios automáticos ───────────────────────────────────
        services.AddScoped<IReminderRepository, EfReminderRepository>();
        services.AddScoped<CreateReminderHandler>();
        services.AddScoped<GetReminderHandler>();
        services.AddScoped<SearchRemindersHandler>();
        services.AddScoped<UpdateReminderHandler>();
        services.AddScoped<DeleteReminderHandler>();
        services.AddScoped<CompleteReminderHandler>();
        services.AddScoped<EvaluateDueRemindersHandler>();

        // ── E14: Agenda y turnos ──────────────────────────────────────────────
        services.AddScoped<ISchedulingRepository, EfSchedulingRepository>();
        services.AddScoped<GetAppointmentsHandler>();
        services.AddScoped<GetAppointmentHandler>();
        services.AddScoped<CreateAppointmentHandler>();
        services.AddScoped<UpdateAppointmentHandler>();
        services.AddScoped<CancelAppointmentHandler>();
        services.AddScoped<ConvertAppointmentToWorkOrderHandler>();
        services.AddScoped<ConfirmAppointmentPublicHandler>();
        services.AddScoped<GetAvailabilitySlotsHandler>();
        services.AddScoped<ManageAvailabilityHandler>();

        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddScoped<TallerControl.Application.Notifications.INotificationRepository, Notifications.EfNotificationRepository>();
        services.AddScoped<TallerControl.Application.Notifications.NotificationsHandler>();
        services.AddHostedService<OutboxDispatcher>();
        services.AddHostedService<Maintenance.MaintenanceWorker>();

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: [ReadyTag]);

        return services;
    }
}
