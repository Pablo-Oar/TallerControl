using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace TallerControl.Api.Logging;

/// <summary>Agrega <c>TraceId</c> a cada evento: el mismo valor que viaja en <c>traceId</c> de los errores.</summary>
public sealed class TraceEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;
        if (activity is null)
        {
            return;
        }

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", activity.TraceId.ToString()));
    }
}
