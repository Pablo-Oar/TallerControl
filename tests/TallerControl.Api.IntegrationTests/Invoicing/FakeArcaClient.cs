using TallerControl.Application.Invoicing;

namespace TallerControl.Api.IntegrationTests.Invoicing;

/// <summary>
/// Stub de IArcaClient para tests de integración.
/// Siempre autoriza con un CAE fijo y número secuencial; nunca llama a ARCA.
/// </summary>
internal sealed class FakeArcaClient : IArcaClient
{
    public const string FakeCae = "70417054367466";
    private int _lastNumber = 0;

    public Task<ArcaTicket> GetTicketAsync(ArcaCertificateCredentials credentials, CancellationToken ct)
    {
        var ticket = new ArcaTicket(
            Token: "FAKE_TOKEN",
            Sign: "FAKE_SIGN",
            Cuit: credentials.Cuit,
            GenerationTime: DateTimeOffset.UtcNow,
            ExpirationTime: DateTimeOffset.UtcNow.AddHours(12));

        return Task.FromResult(ticket);
    }

    public Task<ArcaAuthorizeResult> AuthorizeInvoiceAsync(ArcaTicket ticket, ArcaInvoiceRequest request, CancellationToken ct)
    {
        var number = ++_lastNumber;
        var result = new ArcaAuthorizeResult(
            IsAuthorized: true,
            Number: number,
            Cae: FakeCae,
            CaeExpiresAt: DateTimeOffset.UtcNow.AddDays(5),
            RejectionReason: null,
            RawResponse: "<FakeResponse><CAE>70417054367466</CAE></FakeResponse>");

        return Task.FromResult(result);
    }

    public Task<int> GetLastAuthorizedNumberAsync(ArcaTicket ticket, int pointOfSaleNumber, int invoiceTypeCode, CancellationToken ct)
        => Task.FromResult(_lastNumber);
}
