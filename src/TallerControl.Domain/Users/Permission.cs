namespace TallerControl.Domain.Users;

/// <summary>
/// Permisos evaluables por endpoint y por la UI (doc 07 §3, REQ-03).
/// Los roles traen un conjunto base; el dueño puede agregar permisos finos al mecánico.
/// </summary>
public static class Permission
{
    public const string SettingsManage = "settings.manage";
    public const string UsersManage = "users.manage";

    public const string CustomersRead = "customers.read";
    public const string CustomersWrite = "customers.write";
    public const string CustomersDelete = "customers.delete";
    public const string ContactView = "contact.view";

    public const string VehiclesRead = "vehicles.read";
    public const string VehiclesWrite = "vehicles.write";
    public const string VehiclesOdometerCorrect = "vehicles.odometer.correct";

    public const string CatalogRead = "catalog.read";
    public const string CatalogWrite = "catalog.write";
    public const string StockAdjust = "stock.adjust";
    public const string CostsView = "costs.view";
    public const string PricesView = "prices.view";

    public const string QuotesRead = "quotes.read";
    public const string QuotesWrite = "quotes.write";

    public const string WorkOrdersRead = "workorders.read";
    public const string WorkOrdersWrite = "workorders.write";
    public const string WorkOrdersStatus = "workorders.status";
    public const string WorkOrdersCancel = "workorders.cancel";
    public const string WorkOrdersDeliver = "workorders.deliver";
    public const string WorkOrdersAssignSelf = "workorders.assign_self";

    public const string AttachmentsWrite = "attachments.write";

    public const string PaymentsRead = "payments.read";
    public const string PaymentsWrite = "payments.write";
    public const string PaymentsVoid = "payments.void";

    public const string QrManage = "qr.manage";
    public const string QrRevoke = "qr.revoke";

    public const string CashRead = "cash.read";
    public const string CashOpen = "cash.open";
    public const string CashClose = "cash.close";
    public const string CashAdjust = "cash.adjust";

    public const string SuppliersRead = "suppliers.read";
    public const string SuppliersWrite = "suppliers.write";

    public const string PurchasesRead = "purchases.read";
    public const string PurchasesWrite = "purchases.write";

    public const string CounterSalesRead = "counter_sales.read";
    public const string CounterSalesWrite = "counter_sales.write";

    public const string AuditRead = "audit.read";

    public const string InvoicingRead = "invoicing.read";
    public const string InvoicingWrite = "invoicing.write";
    public const string InvoicingEmit = "invoicing.emit";

    /// <summary>Permisos base del owner: todos.</summary>
    public static IReadOnlySet<string> ForOwner { get; } = new HashSet<string>
    {
        SettingsManage, UsersManage,
        CustomersRead, CustomersWrite, CustomersDelete, ContactView,
        VehiclesRead, VehiclesWrite, VehiclesOdometerCorrect,
        CatalogRead, CatalogWrite, StockAdjust, CostsView, PricesView,
        QuotesRead, QuotesWrite,
        WorkOrdersRead, WorkOrdersWrite, WorkOrdersStatus, WorkOrdersCancel, WorkOrdersDeliver, WorkOrdersAssignSelf,
        AttachmentsWrite,
        PaymentsRead, PaymentsWrite, PaymentsVoid,
        QrManage, QrRevoke,
        CashRead, CashOpen, CashClose, CashAdjust,
        SuppliersRead, SuppliersWrite,
        PurchasesRead, PurchasesWrite,
        CounterSalesRead, CounterSalesWrite,
        AuditRead,
        InvoicingRead, InvoicingWrite, InvoicingEmit,
    };

    /// <summary>Permisos base del administrativo.</summary>
    public static IReadOnlySet<string> ForAdminStaff { get; } = new HashSet<string>
    {
        CustomersRead, CustomersWrite, ContactView,
        VehiclesRead, VehiclesWrite,
        CatalogRead, CatalogWrite, PricesView,
        QuotesRead, QuotesWrite,
        WorkOrdersRead, WorkOrdersWrite, WorkOrdersStatus, WorkOrdersCancel, WorkOrdersDeliver,
        AttachmentsWrite,
        PaymentsRead, PaymentsWrite,
        QrManage,
        CashRead, CashOpen, CashClose, CashAdjust,
        SuppliersRead, SuppliersWrite,
        PurchasesRead, PurchasesWrite,
        CounterSalesRead, CounterSalesWrite,
        InvoicingRead, InvoicingEmit,
    };

    /// <summary>Permisos base del mecánico (sin finos adicionales).</summary>
    public static IReadOnlySet<string> ForMechanic { get; } = new HashSet<string>
    {
        VehiclesRead, VehiclesWrite,
        CatalogRead,
        WorkOrdersRead, WorkOrdersStatus,
        WorkOrdersAssignSelf,
        AttachmentsWrite,
    };

    /// <summary>Permisos finos que el dueño puede otorgar a un mecánico (doc 13 §4).</summary>
    public static IReadOnlySet<string> MechanicFineGrained { get; } = new HashSet<string>
    {
        PricesView,
        ContactView,
    };

    /// <summary>
    /// Contador: solo lectura de facturación; no ocupa lugar del plan (doc 13 §4, E12).
    /// </summary>
    public static IReadOnlySet<string> ForAccountant { get; } = new HashSet<string>
    {
        InvoicingRead,
    };

    public static IReadOnlySet<string> BaseFor(TenantRole role) => role switch
    {
        TenantRole.Owner => ForOwner,
        TenantRole.AdminStaff => ForAdminStaff,
        TenantRole.Mechanic => ForMechanic,
        TenantRole.Accountant => ForAccountant,
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };
}
