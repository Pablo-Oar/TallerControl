using TallerControl.Api.Identity;
using TallerControl.Application.Invoicing;
using TallerControl.Domain.Invoicing;
using TallerControl.Domain.Users;

namespace TallerControl.Api.Endpoints.Invoicing;

/// <summary>
/// Endpoints de facturación electrónica ARCA (E12, doc 13).
///
/// Certificados ARCA (Owner only — configuración del taller):
/// GET    /api/v1/invoicing/certificates                → invoicing.read  (O, A, Contador)
/// POST   /api/v1/invoicing/certificates                → invoicing.write (O)
/// POST   /api/v1/invoicing/certificates/{id}/upload    → invoicing.write (O)
/// POST   /api/v1/invoicing/certificates/{id}/test      → invoicing.write (O)
///
/// Puntos de venta:
/// GET    /api/v1/invoicing/points-of-sale              → invoicing.read  (O, A, Contador)
/// POST   /api/v1/invoicing/points-of-sale              → invoicing.write (O)
/// PUT    /api/v1/invoicing/points-of-sale/{id}         → invoicing.write (O)
///
/// Facturas:
/// GET    /api/v1/invoicing/invoices                    → invoicing.read  (O, A, Contador)
/// GET    /api/v1/invoicing/invoices/{id}               → invoicing.read  (O, A, Contador)
/// POST   /api/v1/work-orders/{id}/invoice              → invoicing.emit  (O, A)
/// POST   /api/v1/counter-sales/{id}/invoice            → invoicing.emit  (O, A)
/// POST   /api/v1/invoicing/invoices/{id}/void          → invoicing.emit  (O, A)
///
/// Libro IVA / CITI:
/// GET    /api/v1/invoicing/invoices/{id}/pdf           → invoicing.read  (O, A, Contador)
/// GET    /api/v1/invoicing/iva-book                    → invoicing.read  (O, A, Contador)
/// GET    /api/v1/invoicing/citi-export                 → invoicing.read  (O, A, Contador)
/// </summary>
internal static class InvoicingEndpoints
{
    public static IEndpointRouteBuilder MapInvoicingEndpoints(this IEndpointRouteBuilder app)
    {
        // ── Certificados ──────────────────────────────────────────────────────

        app.MapGet("/api/v1/invoicing/certificates",
                (ListFiscalCertificatesHandler handler, CancellationToken ct) =>
                    handler.HandleAsync(ct))
            .RequirePermission(Permission.InvoicingRead);

        app.MapPost("/api/v1/invoicing/certificates",
                (CreateFiscalCertificateRequest request, CreateFiscalCertificateHandler handler, CancellationToken ct) =>
                    CreateCertificateAsync(request, handler, ct))
            .RequirePermission(Permission.InvoicingWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPost("/api/v1/invoicing/certificates/{id:guid}/upload",
                (Guid id, UploadFiscalCertificateRequest request, UploadFiscalCertificateHandler handler, CancellationToken ct) =>
                    UploadCertificateAsync(id, request, handler, ct))
            .RequirePermission(Permission.InvoicingWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPost("/api/v1/invoicing/certificates/{id:guid}/revoke",
                async (Guid id, RevokeFiscalCertificateHandler handler, CancellationToken ct) =>
                    Results.Ok(await handler.HandleAsync(id, ct)))
            .RequirePermission(Permission.InvoicingWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPost("/api/v1/invoicing/certificates/{id:guid}/renew",
                async (Guid id, RenewFiscalCertificateHandler handler, CancellationToken ct) =>
                    Results.Ok(await handler.HandleAsync(id, ct)))
            .RequirePermission(Permission.InvoicingWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPost("/api/v1/invoicing/certificates/{id:guid}/test",
                (Guid id, TestFiscalConnectionHandler handler, CancellationToken ct,
                 Guid? pointOfSaleId = null) =>
                    TestConnectionAsync(id, pointOfSaleId, handler, ct))
            .RequirePermission(Permission.InvoicingWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        // ── Puntos de Venta ───────────────────────────────────────────────────

        app.MapGet("/api/v1/invoicing/points-of-sale",
                (ListFiscalPointsOfSaleHandler handler, CancellationToken ct) =>
                    handler.HandleAsync(ct))
            .RequirePermission(Permission.InvoicingRead);

        app.MapPost("/api/v1/invoicing/points-of-sale",
                (CreateFiscalPointOfSaleRequest request, CreateFiscalPointOfSaleHandler handler, CancellationToken ct) =>
                    CreatePosAsync(request, handler, ct))
            .RequirePermission(Permission.InvoicingWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPut("/api/v1/invoicing/points-of-sale/{id:guid}",
                (Guid id, UpdateFiscalPointOfSaleRequest request, HttpRequest req,
                 UpdateFiscalPointOfSaleHandler handler, CancellationToken ct) =>
                    UpdatePosAsync(id, request, req, handler, ct))
            .RequirePermission(Permission.InvoicingWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        // ── Facturas ──────────────────────────────────────────────────────────

        app.MapGet("/api/v1/invoicing/invoices",
                (SearchInvoicesHandler handler, CancellationToken ct,
                 string? status = null, string? sourceType = null, Guid? sourceId = null, int page = 1, int pageSize = 25) =>
                    SearchInvoicesAsync(status, sourceType, sourceId, page, pageSize, handler, ct))
            .RequirePermission(Permission.InvoicingRead);

        app.MapGet("/api/v1/invoicing/invoices/{id:guid}",
                (Guid id, GetInvoiceHandler handler, CancellationToken ct) =>
                    handler.HandleAsync(id, ct))
            .RequirePermission(Permission.InvoicingRead);

        app.MapGet("/api/v1/invoicing/invoices/{id:guid}/pdf",
                async (Guid id, GetInvoicePdfHandler handler, CancellationToken ct) =>
                {
                    var (bytes, fileName) = await handler.HandleAsync(id, ct);
                    return Results.File(bytes, "application/pdf", fileName);
                })
            .RequirePermission(Permission.InvoicingRead);

        app.MapPost("/api/v1/work-orders/{id:guid}/invoice",
                (Guid id, EmitInvoiceRequest request, EmitInvoiceHandler handler, CancellationToken ct) =>
                    EmitInvoiceAsync(InvoiceSourceType.WorkOrder, id, request, handler, ct))
            .RequirePermission(Permission.InvoicingEmit)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPost("/api/v1/counter-sales/{id:guid}/invoice",
                (Guid id, EmitInvoiceRequest request, EmitInvoiceHandler handler, CancellationToken ct) =>
                    EmitInvoiceAsync(InvoiceSourceType.CounterSale, id, request, handler, ct))
            .RequirePermission(Permission.InvoicingEmit)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPost("/api/v1/invoicing/invoices/{id:guid}/void",
                (Guid id, VoidInvoiceHandler handler, CancellationToken ct) =>
                    VoidAsync(id, handler, ct))
            .RequirePermission(Permission.InvoicingEmit)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        // ── Libro IVA / CITI ──────────────────────────────────────────────────

        app.MapGet("/api/v1/invoicing/iva-book",
                (GetIvaBookHandler handler, CancellationToken ct, int year = 0, int month = 0) =>
                    GetIvaBookAsync(year, month, handler, ct))
            .RequirePermission(Permission.InvoicingRead);

        app.MapGet("/api/v1/invoicing/citi-export",
                (GetCitiExportHandler handler, CancellationToken ct, int year = 0, int month = 0) =>
                    GetCitiAsync(year, month, handler, ct))
            .RequirePermission(Permission.InvoicingRead);

        return app;
    }

    private static async Task<IResult> CreateCertificateAsync(
        CreateFiscalCertificateRequest request, CreateFiscalCertificateHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(request, ct);
        return Results.Created($"/api/v1/invoicing/certificates/{dto.Id}", dto);
    }

    private static async Task<IResult> UploadCertificateAsync(
        Guid id, UploadFiscalCertificateRequest request, UploadFiscalCertificateHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(id, request, ct);
        return Results.Ok(dto);
    }

    private static async Task<IResult> TestConnectionAsync(
        Guid id, Guid? pointOfSaleId, TestFiscalConnectionHandler handler, CancellationToken ct)
    {
        if (pointOfSaleId == null)
            return Results.BadRequest(new { message = "Se requiere el parámetro pointOfSaleId." });
        var result = await handler.HandleAsync(pointOfSaleId.Value, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreatePosAsync(
        CreateFiscalPointOfSaleRequest request, CreateFiscalPointOfSaleHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(request, ct);
        return Results.Created($"/api/v1/invoicing/points-of-sale/{dto.Id}", dto);
    }

    private static async Task<IResult> UpdatePosAsync(
        Guid id, UpdateFiscalPointOfSaleRequest request, HttpRequest req,
        UpdateFiscalPointOfSaleHandler handler, CancellationToken ct)
    {
        if (!uint.TryParse(req.Headers["If-Match"].FirstOrDefault()?.Trim('"'), out var version))
            return Results.StatusCode(412);
        var dto = await handler.HandleAsync(id, request, version, ct);
        return Results.Ok(dto);
    }

    private static Task<IResult> SearchInvoicesAsync(
        string? status, string? sourceType, Guid? sourceId, int page, int pageSize,
        SearchInvoicesHandler handler, CancellationToken ct)
    {
        InvoiceStatus? statusEnum = null;
        if (!string.IsNullOrEmpty(status))
        {
            if (!EnumQuery.TryParse<InvoiceStatus>(status, out var s))
                return Task.FromResult(Results.BadRequest(new { message = $"Estado inválido: {status}" }));
            statusEnum = s;
        }

        InvoiceSourceType? sourceTypeEnum = null;
        if (!string.IsNullOrEmpty(sourceType))
        {
            if (!EnumQuery.TryParse<InvoiceSourceType>(sourceType, out var st))
                return Task.FromResult(Results.BadRequest(new { message = $"Tipo de fuente inválido: {sourceType}" }));
            sourceTypeEnum = st;
        }

        return ExecuteSearchAsync(statusEnum, sourceTypeEnum, sourceId, page, pageSize, handler, ct);
    }

    private static async Task<IResult> ExecuteSearchAsync(
        InvoiceStatus? status, InvoiceSourceType? sourceType, Guid? sourceId, int page, int pageSize,
        SearchInvoicesHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(status, sourceType, sourceId, page, pageSize, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> EmitInvoiceAsync(
        InvoiceSourceType sourceType, Guid sourceId,
        EmitInvoiceRequest request, EmitInvoiceHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(sourceType, sourceId, request, ct);
        return Results.Created($"/api/v1/invoicing/invoices/{dto.Id}", dto);
    }

    private static async Task<IResult> VoidAsync(
        Guid id, VoidInvoiceHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(id, ct);
        return Results.Created($"/api/v1/invoicing/invoices/{dto.Id}", dto);
    }

    private static async Task<IResult> GetIvaBookAsync(
        int year, int month, GetIvaBookHandler handler, CancellationToken ct)
    {
        if (year == 0 || month == 0)
            return Results.BadRequest(new { message = "Se requieren los parámetros year y month." });
        var dto = await handler.HandleAsync(year, month, ct);
        return Results.Ok(dto);
    }

    private static async Task<IResult> GetCitiAsync(
        int year, int month, GetCitiExportHandler handler, CancellationToken ct)
    {
        if (year == 0 || month == 0)
            return Results.BadRequest(new { message = "Se requieren los parámetros year y month." });
        var (zipBytes, fileName) = await handler.HandleAsync(year, month, ct);
        return Results.File(zipBytes, "application/zip", fileName);
    }
}
