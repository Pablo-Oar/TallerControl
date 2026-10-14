using System.Security.Cryptography;
using TallerControl.Application.Common;

namespace TallerControl.Application.Payments;

/// <summary>
/// Caso de uso: obtener comprobante público por token (doc 05 §2.9, T-PUB-07, REQ-15).
/// Superficie pública: solo datos mínimos, sin datos personales del taller.
/// </summary>
public sealed class GetPublicPaymentHandler
{
    private readonly IPaymentRepository _repo;

    public GetPublicPaymentHandler(IPaymentRepository repo)
    {
        _repo = repo;
    }

    public async Task<PublicPaymentDto> HandleAsync(string token, CancellationToken ct)
    {
        byte[] tokenBytes;
        try
        {
            var padded = token.Replace('-', '+').Replace('_', '/');
            while (padded.Length % 4 != 0) padded += '=';
            tokenBytes = Convert.FromBase64String(padded);
        }
        catch
        {
            throw new NotFoundException("El comprobante no existe.");
        }

        var hash = SHA256.HashData(tokenBytes);
        var payment = await _repo.FindByTokenHashAsync(hash, ct)
            ?? throw new NotFoundException("El comprobante no existe.");

        return new PublicPaymentDto(
            payment.Number,
            payment.Method,
            payment.Amount,
            payment.PaidAt,
            payment.Reference,
            payment.Status);
    }
}
