using System.Diagnostics;

namespace TallerControl.Api.Errors;

public static class ErrorHandlingExtensions
{
    /// <summary>
    /// Respuestas de error uniformes <c>application/problem+json</c> con <c>traceId</c> (doc 02 §9).
    /// El mismo identificador aparece en los logs como <c>TraceId</c>.
    /// </summary>
    public static IServiceCollection AddApiErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions["traceId"] =
                    Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier);

        services.AddExceptionHandler<ApiExceptionHandler>();
        return services;
    }

    /// <summary>
    /// El orden importa: el log de la solicitud envuelve al manejador de errores para ver el estado final.
    /// Compartido por <c>Program</c> y las pruebas para que no se desvíen.
    /// </summary>
    public static IApplicationBuilder UseApiErrorPipeline(this IApplicationBuilder app)
    {
        app.UseMiddleware<Logging.RequestLoggingMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        return app;
    }
}
