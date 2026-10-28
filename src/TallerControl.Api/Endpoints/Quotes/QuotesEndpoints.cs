using TallerControl.Api.Identity;
using TallerControl.Application.Common;
using TallerControl.Application.Quotes;
using TallerControl.Domain.Quotes;
using TallerControl.Domain.Users;

namespace TallerControl.Api.Endpoints.Quotes;

/// <summary>
/// Endpoints de presupuestos (doc 05 §2.6, REQ-10, REQ-11, REQ-16).
/// GET    /api/v1/quotes                          → quotes.read
/// POST   /api/v1/quotes                          → quotes.write
/// GET    /api/v1/quotes/{id}                     → quotes.read
/// PUT    /api/v1/quotes/{id}                     → quotes.write + If-Match
/// POST   /api/v1/quotes/{id}/send                → quotes.write
/// POST   /api/v1/quotes/{id}/reopen              → quotes.write
/// POST   /api/v1/quotes/{id}/convert             → quotes.write
/// POST   /api/v1/quotes/{id}/cancel              → quotes.write
/// DELETE /api/v1/quotes/{id}                     → quotes.write
/// GET    /api/v1/quotes/{id}/responses           → quotes.read
/// GET    /api/v1/quotes/{id}/responses/{responseId}/signer-document → audit.read (O)
/// GET    /api/v1/quotes/{id}/pdf                 → quotes.read
/// </summary>
internal static class QuotesEndpoints
{
    public static IEndpointRouteBuilder MapQuotesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/quotes",
                (SearchQuotesHandler handler, CancellationToken ct,
                 string? status = null, Guid? customerId = null, Guid? vehicleId = null,
                 DateOnly? from = null, DateOnly? to = null,
                 string? sort = null, string? dir = null,
                 int page = 1, int pageSize = 25) =>
                {
                    QuoteStatus? parsedStatus = status is not null
                        && EnumQuery.TryParse<QuoteStatus>(status, out var s)
                        ? s : null;
                    return handler.HandleAsync(parsedStatus, customerId, vehicleId, from, to, page, pageSize, SortSpec.Parse(sort, dir), ct);
                })
            .RequirePermission(Permission.QuotesRead);

        app.MapPost("/api/v1/quotes",
                (CreateQuoteRequest request, CreateQuoteHandler handler, CancellationToken ct) =>
                    CreateAsync(request, handler, ct))
            .RequirePermission(Permission.QuotesWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapGet("/api/v1/quotes/{id:guid}",
                (Guid id, GetQuoteHandler handler, CancellationToken ct) =>
                    GetAsync(id, handler, ct))
            .RequirePermission(Permission.QuotesRead);

        app.MapPut("/api/v1/quotes/{id:guid}",
                (Guid id, UpdateQuoteRequest request, UpdateQuoteHandler handler,
                 HttpRequest httpRequest, CancellationToken ct) =>
                    UpdateAsync(id, request, handler, httpRequest, ct))
            .RequirePermission(Permission.QuotesWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPost("/api/v1/quotes/{id:guid}/send",
                (Guid id, SendQuoteHandler handler, CancellationToken ct) =>
                    SendAsync(id, handler, ct))
            .RequirePermission(Permission.QuotesWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPost("/api/v1/quotes/{id:guid}/public-link",
                (Guid id, GetQuotePublicLinkHandler handler, CancellationToken ct) =>
                    handler.HandleAsync(id, ct))
            .RequirePermission(Permission.QuotesWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPost("/api/v1/quotes/{id:guid}/reopen",
                (Guid id, ReopenQuoteRequest request, ReopenQuoteHandler handler, CancellationToken ct) =>
                    ReopenAsync(id, request, handler, ct))
            .RequirePermission(Permission.QuotesWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPost("/api/v1/quotes/{id:guid}/convert",
                (Guid id, ConvertQuoteRequest request, ConvertQuoteHandler handler, CancellationToken ct) =>
                    ConvertAsync(id, request, handler, ct))
            .RequirePermission(Permission.QuotesWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPost("/api/v1/quotes/{id:guid}/cancel",
                (Guid id, CancelQuoteRequest request, CancelQuoteHandler handler, CancellationToken ct) =>
                    CancelAsync(id, request, handler, ct))
            .RequirePermission(Permission.QuotesWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapDelete("/api/v1/quotes/{id:guid}",
                (Guid id, [Microsoft.AspNetCore.Mvc.FromBody] DeleteQuoteRequest request,
                 DeleteQuoteHandler handler, CancellationToken ct) =>
                    DeleteAsync(id, request.Reason, handler, ct))
            .RequirePermission(Permission.QuotesWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapGet("/api/v1/quotes/{id:guid}/responses",
                (Guid id, GetQuoteResponsesHandler handler, CancellationToken ct) =>
                    ResponsesAsync(id, handler, ct))
            .RequirePermission(Permission.QuotesRead);

        app.MapGet("/api/v1/quotes/{id:guid}/responses/{responseId:guid}/signer-document",
                async (Guid id, Guid responseId, GetSignerDocumentHandler handler, ILogger<GetSignerDocumentHandler> logger, CancellationToken ct) =>
                {
                    var dto = await handler.HandleAsync(id, responseId, ct);
                    logger.LogInformation("Se consultó el documento del firmante de la respuesta {ResponseId} del presupuesto {QuoteId}", responseId, id);
                    return Results.Ok(dto);
                })
            .RequirePermission(Permission.AuditRead);

        app.MapGet("/api/v1/quotes/{id:guid}/pdf",
                (Guid id, GetQuotePdfHandler handler, CancellationToken ct) =>
                    PdfAsync(id, handler, ct))
            .RequirePermission(Permission.QuotesRead);

        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreateQuoteRequest request, CreateQuoteHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(request, ct);
        return Results.Created($"/api/v1/quotes/{dto.Id}", dto);
    }

    private static async Task<IResult> GetAsync(
        Guid id, GetQuoteHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(id, ct);
        return Results.Ok(dto);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id, UpdateQuoteRequest request, UpdateQuoteHandler handler,
        HttpRequest httpRequest, CancellationToken ct)
    {
        if (!TryParseETag(httpRequest.Headers.IfMatch.FirstOrDefault(), out var version))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Cabecera requerida",
                detail: "Se requiere la cabecera If-Match con la versión del recurso.");
        }

        var dto = await handler.HandleAsync(id, version, request, ct);
        return Results.Ok(dto);
    }

    private static async Task<IResult> SendAsync(
        Guid id, SendQuoteHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(id, ct);
        return Results.Ok(dto);
    }

    private static async Task<IResult> ReopenAsync(
        Guid id, ReopenQuoteRequest request, ReopenQuoteHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(id, request, ct);
        return Results.Ok(dto);
    }

    private static async Task<IResult> ConvertAsync(
        Guid id, ConvertQuoteRequest request, ConvertQuoteHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(id, request, ct);
        return Results.Created($"/api/v1/work-orders/{dto.Id}", dto);
    }

    private static async Task<IResult> CancelAsync(
        Guid id, CancelQuoteRequest request, CancelQuoteHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(id, request, ct);
        return Results.Ok(dto);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id, string? reason, DeleteQuoteHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(id, reason, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ResponsesAsync(
        Guid id, GetQuoteResponsesHandler handler, CancellationToken ct)
    {
        var list = await handler.HandleAsync(id, ct);
        return Results.Ok(list);
    }

    private static async Task<IResult> PdfAsync(
        Guid id, GetQuotePdfHandler handler, CancellationToken ct)
    {
        var (bytes, number) = await handler.HandleAsync(id, ct);
        return Results.File(bytes, "application/pdf", $"presupuesto-{number}.pdf");
    }

    private static bool TryParseETag(string? raw, out uint version)
    {
        version = 0;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        return uint.TryParse(raw.Trim('"'), out version);
    }
}
