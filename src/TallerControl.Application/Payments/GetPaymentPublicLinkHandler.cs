using System.Security.Cryptography;
using TallerControl.Application.Common;

namespace TallerControl.Application.Payments;

/// <summary>
/// Caso de uso: entregar el enlace público del comprobante de un cobro (F-03). El token solo se guarda como hash,
/// así que para mostrarlo de nuevo se emite uno nuevo y el anterior deja de servir.
/// </summary>
public sealed class GetPaymentPublicLinkHandler
{
    private readonly IPaymentRepository _repo;

    public GetPaymentPublicLinkHandler(IPaymentRepository repo) => _repo = repo;

    public async Task<PaymentPublicLinkDto> HandleAsync(Guid paymentId, CancellationToken ct)
    {
        var payment = await _repo.FindByIdAsync(paymentId, ct)
            ?? throw new NotFoundException("El pago no existe.");

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        payment.ReplacePublicToken(SHA256.HashData(tokenBytes));
        await _repo.SaveChangesAsync(ct);

        var token = Convert.ToBase64String(tokenBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return new PaymentPublicLinkDto(token);
    }
}

public sealed record PaymentPublicLinkDto(string PublicToken);
