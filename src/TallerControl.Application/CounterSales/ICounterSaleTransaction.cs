namespace TallerControl.Application.CounterSales;

/// <summary>
/// Abstracción de transacción para crear ventas de mostrador.
/// <see cref="IDocumentNumberGenerator"/> exige una transacción activa para la numeración.
/// </summary>
public interface ICounterSaleTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);
}
