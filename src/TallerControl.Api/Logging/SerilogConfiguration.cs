using Serilog;
using Serilog.Formatting.Compact;

namespace TallerControl.Api.Logging;

public static class SerilogConfiguration
{
    /// <summary>
    /// Logs estructurados a la consola (doc 02 §9): JSON compacto fuera de Development, texto legible
    /// en Development. Niveles en la sección <c>Serilog</c> de la configuración.
    /// Regla: nunca registrar contraseñas, OTP, tokens ni DNI; las rutas pasan por <see cref="LogSanitizer"/>.
    /// </summary>
    public static void Configure(HostBuilderContext context, LoggerConfiguration logger)
    {
        logger
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .Enrich.With<TraceEnricher>()
            .Enrich.WithProperty("Application", "TallerControl.Api");

        if (context.HostingEnvironment.IsDevelopment())
        {
            logger.WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
        }
        else
        {
            logger.WriteTo.Console(new CompactJsonFormatter());
        }
    }
}
