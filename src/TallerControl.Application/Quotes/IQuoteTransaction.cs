namespace TallerControl.Application.Quotes;

/// <summary>
/// Abstracción de transacción para crear presupuestos.
/// El <see cref="TallerControl.Application.Tenancy.IDocumentNumberGenerator"/> exige que la numeración
/// ocurra dentro de una transacción activa; esta interfaz oculta los detalles de EF Core al handler.
/// </summary>
public interface IQuoteTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);
}
