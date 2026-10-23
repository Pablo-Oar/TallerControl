using System.Collections.Concurrent;
using TallerControl.Application.Push;

namespace TallerControl.Api.IntegrationTests.Push;

/// <summary>Reemplaza el envío push real en las pruebas: anota a quién se le mandó cada aviso.</summary>
public sealed class RecordingPushSender : IPushSender
{
    public ConcurrentBag<(Guid UserId, PushMessage Message)> Sent { get; } = [];

    public string? PublicKey => "clave-de-prueba";

    public Task<PushResult> SendToUserAsync(Guid userId, PushMessage message, CancellationToken ct)
    {
        Sent.Add((userId, message));
        return Task.FromResult(new PushResult(1, 1, [], [201]));
    }
}
