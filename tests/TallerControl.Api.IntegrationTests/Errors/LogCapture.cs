using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace TallerControl.Api.IntegrationTests.Errors;

internal static class LogCapture
{
    private static readonly ConcurrentQueue<LogEvent> _events = new();

    public static ILogEventSink Sink { get; } = new CapturingSink();

    public static IReadOnlyCollection<LogEvent> Events => _events.ToArray();

    public static void Clear()
    {
        while (_events.TryDequeue(out _))
        {
        }
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);
    }
}
