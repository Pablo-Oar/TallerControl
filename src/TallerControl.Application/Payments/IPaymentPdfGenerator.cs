namespace TallerControl.Application.Payments;

public interface IPaymentPdfGenerator
{
    Task<byte[]> GenerateAsync(Guid paymentId, CancellationToken ct);
}
