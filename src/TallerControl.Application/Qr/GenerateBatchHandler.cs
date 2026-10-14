using TallerControl.Application.Common;
using TallerControl.Domain.Qr;

namespace TallerControl.Application.Qr;

public sealed class GenerateBatchHandler(IQrRepository repo, ITenantContext tenant, TimeProvider clock)
{
    public const int MaxBatchSize = 500;

    public async Task<GenerateBatchResult> HandleAsync(GenerateBatchRequest request, CancellationToken ct)
    {
        if (request.Quantity is < 1 or > MaxBatchSize)
            throw new BusinessRuleException($"La cantidad debe estar entre 1 y {MaxBatchSize}.");

        var tenantId = tenant.TenantId ?? throw new InvalidOperationException("TenantId requerido.");
        var batchId = Guid.CreateVersion7();
        var now = clock.GetUtcNow();
        var cards = new List<QrCard>(request.Quantity);

        for (var i = 0; i < request.Quantity; i++)
            cards.Add(new QrCard(tenantId, QrCodeGenerator.GenerateCode(), batchId, now));

        await repo.AddRangeAsync(cards, ct);
        await repo.SaveChangesAsync(ct);

        return new GenerateBatchResult(batchId, cards.Count);
    }
}
