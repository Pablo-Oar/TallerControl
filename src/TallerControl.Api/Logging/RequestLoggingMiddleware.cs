using System.Diagnostics;
using Serilog.Context;
using TallerControl.Application.Common;

namespace TallerControl.Api.Logging;

/// <summary>
/// Un evento por solicitud, con la ruta saneada (sin tokens ni query string) y el taller. Además
/// deja <c>tenant_id</c> en el contexto de log de todo lo que ocurra durante la solicitud.
/// Va antes del manejador de excepciones para registrar el código de estado final.
/// </summary>
public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var tenantId = context.RequestServices.GetService<ITenantContext>()?.TenantId;
        var started = Stopwatch.GetTimestamp();

        using (LogContext.PushProperty("tenant_id", tenantId))
        {
            try
            {
                await _next(context);
            }
            finally
            {
                var status = context.Response.StatusCode;
                var path = LogSanitizer.SanitizePath(context.Request.Path.Value);

                // Los health checks se consultan cada pocos segundos: no ensucian el log.
                var level = status >= StatusCodes.Status500InternalServerError
                    ? LogLevel.Error
                    : path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ? LogLevel.Debug : LogLevel.Information;

                _logger.Log(
                    level,
                    "HTTP {Method} {Path} respondió {StatusCode} en {ElapsedMs} ms",
                    context.Request.Method,
                    path,
                    status,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
        }
    }
}
