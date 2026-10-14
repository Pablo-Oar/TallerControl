namespace TallerControl.Application.Work;

/// <summary>
/// Abstracción de transacción para crear órdenes de trabajo.
/// El <see cref="IDocumentNumberGenerator"/> exige que la numeración ocurra dentro
/// de una transacción activa; esta interfaz oculta los detalles de EF Core al handler.
/// </summary>
public interface IWorkOrderTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);
}
