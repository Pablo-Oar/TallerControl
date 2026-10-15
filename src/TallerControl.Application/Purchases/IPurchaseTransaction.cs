namespace TallerControl.Application.Purchases;

/// <summary>
/// Abstracción de transacción para crear compras.
/// <see cref="IDocumentNumberGenerator"/> exige una transacción activa para la numeración.
/// </summary>
public interface IPurchaseTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);
}
