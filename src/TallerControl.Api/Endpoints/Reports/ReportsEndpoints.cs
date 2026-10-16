using TallerControl.Api.Identity;
using TallerControl.Application.Reports;
using TallerControl.Domain.Users;

namespace TallerControl.Api.Endpoints.Reports;

/// <summary>
/// Reportes básicos y exportación Excel (E13, RF-R5).
/// GET /api/v1/reports/revenue?from=&amp;to=&amp;format=xlsx
/// GET /api/v1/reports/receivables?format=xlsx
/// GET /api/v1/reports/stock?format=xlsx
/// GET /api/v1/reports/purchases?from=&amp;to=&amp;format=xlsx
/// Todos requieren workorders.read AND payments.read (Owner y AdminStaff).
/// </summary>
internal static class ReportsEndpoints
{
    private const string ExcelMimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static IEndpointRouteBuilder MapReportsEndpoints(this IEndpointRouteBuilder app)
    {
        var reportsPolicy = (Action<Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder>)(policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.AddRequirements(new HasPermissionRequirement(Permission.WorkOrdersRead));
            policy.AddRequirements(new HasPermissionRequirement(Permission.PaymentsRead));
        });

        app.MapGet("/api/v1/reports/revenue",
                (string? from, string? to, string? format,
                 GetRevenueReportHandler handler, IReportExporter exporter, CancellationToken ct) =>
                    GetRevenueAsync(from, to, format, handler, exporter, ct))
            .RequireAuthorization(reportsPolicy);

        app.MapGet("/api/v1/reports/receivables",
                (string? format,
                 GetReceivablesReportHandler handler, IReportExporter exporter, CancellationToken ct) =>
                    GetReceivablesAsync(format, handler, exporter, ct))
            .RequireAuthorization(reportsPolicy);

        app.MapGet("/api/v1/reports/stock",
                (string? format,
                 GetStockReportHandler handler, IReportExporter exporter, CancellationToken ct) =>
                    GetStockAsync(format, handler, exporter, ct))
            .RequireAuthorization(reportsPolicy);

        app.MapGet("/api/v1/reports/purchases",
                (string? from, string? to, string? format,
                 GetPurchasesReportHandler handler, IReportExporter exporter, CancellationToken ct) =>
                    GetPurchasesAsync(from, to, format, handler, exporter, ct))
            .RequireAuthorization(reportsPolicy);

        return app;
    }

    private static async Task<IResult> GetRevenueAsync(
        string? from, string? to, string? format,
        GetRevenueReportHandler handler, IReportExporter exporter, CancellationToken ct)
    {
        var (f, t) = ParseDateRange(from, to);
        var report = await handler.HandleAsync(f, t, ct);
        return format == "xlsx"
            ? Results.File(exporter.ExportRevenue(report), ExcelMimeType, $"ingresos_{f:yyyyMMdd}_{t:yyyyMMdd}.xlsx")
            : Results.Ok(report);
    }

    private static async Task<IResult> GetReceivablesAsync(
        string? format,
        GetReceivablesReportHandler handler, IReportExporter exporter, CancellationToken ct)
    {
        var report = await handler.HandleAsync(ct);
        return format == "xlsx"
            ? Results.File(exporter.ExportReceivables(report), ExcelMimeType, "cuentas_por_cobrar.xlsx")
            : Results.Ok(report);
    }

    private static async Task<IResult> GetStockAsync(
        string? format,
        GetStockReportHandler handler, IReportExporter exporter, CancellationToken ct)
    {
        var report = await handler.HandleAsync(ct);
        return format == "xlsx"
            ? Results.File(exporter.ExportStock(report), ExcelMimeType, "stock.xlsx")
            : Results.Ok(report);
    }

    private static async Task<IResult> GetPurchasesAsync(
        string? from, string? to, string? format,
        GetPurchasesReportHandler handler, IReportExporter exporter, CancellationToken ct)
    {
        var (f, t) = ParseDateRange(from, to);
        var report = await handler.HandleAsync(f, t, ct);
        return format == "xlsx"
            ? Results.File(exporter.ExportPurchases(report), ExcelMimeType, $"compras_{f:yyyyMMdd}_{t:yyyyMMdd}.xlsx")
            : Results.Ok(report);
    }

    private static (DateOnly from, DateOnly to) ParseDateRange(string? from, string? to)
    {
        var now = DateOnly.FromDateTime(DateTime.UtcNow);
        var f = DateOnly.TryParse(from, out var pf) ? pf : now.AddMonths(-1);
        var t = DateOnly.TryParse(to, out var pt) ? pt : now;
        return (f, t);
    }
}
