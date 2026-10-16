using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TallerControl.Application.Invoicing;

namespace TallerControl.Infrastructure.Invoicing;

/// <summary>
/// Adaptador ARCA usando SOAP manual vía HttpClient (sin WCF).
/// RT-09: todo el XML SOAP/AFIP está aislado aquí para absorber cambios regulatorios.
/// </summary>
internal sealed class ArcaClient : IArcaClient
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ArcaOptions _options;
    private readonly ILogger<ArcaClient> _logger;

    public ArcaClient(HttpClient http, IMemoryCache cache, IOptions<ArcaOptions> options, ILogger<ArcaClient> logger)
    {
        _http = http;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ArcaTicket> GetTicketAsync(ArcaCertificateCredentials credentials, CancellationToken ct)
    {
        var cacheKey = $"arca_ticket_{credentials.Cuit}_{_options.WsId}";

        if (_cache.TryGetValue(cacheKey, out ArcaTicket? cached) && cached != null)
            return cached;

        _logger.LogInformation("Obteniendo ticket WSAA para CUIT {Cuit}", credentials.Cuit);

        var tra = BuildLoginTicketRequest(_options.WsId);
        var signedCms = SignCms(tra, credentials.PrivateKeyDer, credentials.CertificatePem);
        var cmsSigned = Convert.ToBase64String(signedCms);

        var soapBody = BuildWsaaSoapRequest(cmsSigned);
        var response = await PostSoapAsync(_options.WsaaUrl, soapBody, "loginCms", ct);

        var ticket = ParseWsaaResponse(response, credentials.Cuit);

        var expiry = ticket.ExpirationTime - DateTimeOffset.UtcNow - TimeSpan.FromMinutes(5);
        var cacheDuration = expiry < TimeSpan.Zero
            ? TimeSpan.FromMinutes(1)
            : TimeSpan.FromMinutes(Math.Min(_options.TicketCacheDurationMinutes, expiry.TotalMinutes));

        _cache.Set(cacheKey, ticket, cacheDuration);
        return ticket;
    }

    public async Task<ArcaAuthorizeResult> AuthorizeInvoiceAsync(ArcaTicket ticket, ArcaInvoiceRequest request, CancellationToken ct)
    {
        _logger.LogInformation(
            "FECAESolicitar POS={Pos} Type={Type} CUIT={Cuit}",
            request.PointOfSaleNumber, request.InvoiceTypeCode, request.TenantCuit);

        var lastNumber = await GetLastAuthorizedNumberAsync(ticket, request.PointOfSaleNumber, request.InvoiceTypeCode, ct);
        var nextNumber = lastNumber + 1;

        var soapBody = BuildFecaeSolicitar(ticket, request, nextNumber);
        var rawResponse = await PostSoapAsync(_options.WsfevUrl, soapBody, "FECAESolicitar", ct);

        return ParseFecaeResponse(rawResponse, nextNumber);
    }

    public async Task<int> GetLastAuthorizedNumberAsync(ArcaTicket ticket, int pointOfSaleNumber, int invoiceTypeCode, CancellationToken ct)
    {
        var soapBody = BuildFECompUltimoAutorizado(ticket, pointOfSaleNumber, invoiceTypeCode);
        var rawResponse = await PostSoapAsync(_options.WsfevUrl, soapBody, "FECompUltimoAutorizado", ct);

        var doc = XDocument.Parse(rawResponse);
        var cbteNro = doc.Descendants("CbteNro").FirstOrDefault()?.Value;
        return int.TryParse(cbteNro, out var n) ? n : 0;
    }

    // ── WSAA ──────────────────────────────────────────────────────────────────

    private static string BuildLoginTicketRequest(string wsId)
    {
        var uniqueId = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var generationTime = DateTimeOffset.UtcNow.AddMinutes(-10).ToString("o");
        var expirationTime = DateTimeOffset.UtcNow.AddMinutes(10).ToString("o");

        return $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <loginTicketRequest version="1.0">
                  <header>
                    <uniqueId>{uniqueId}</uniqueId>
                    <generationTime>{generationTime}</generationTime>
                    <expirationTime>{expirationTime}</expirationTime>
                  </header>
                  <service>{wsId}</service>
                </loginTicketRequest>
                """;
    }

    private static byte[] SignCms(string tra, byte[] privateKeyDer, string certificatePem)
    {
        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(privateKeyDer, out _);

        var cert = X509Certificate2.CreateFromPem(certificatePem);
        var certWithKey = cert.CopyWithPrivateKey(rsa);

        var contentInfo = new ContentInfo(Encoding.UTF8.GetBytes(tra));
        var cms = new SignedCms(contentInfo, detached: false);
        var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, certWithKey)
        {
            DigestAlgorithm = new Oid("2.16.840.1.101.3.4.2.1"), // SHA-256
            IncludeOption = X509IncludeOption.EndCertOnly,
        };

        cms.ComputeSignature(signer);
        return cms.Encode();
    }

    private static string BuildWsaaSoapRequest(string cmsSigned) =>
        $"""
         <?xml version="1.0" encoding="UTF-8"?>
         <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:wsaa="http://wsaa.view.sua.dvadac.desein.afip.gov/">
           <soapenv:Header/>
           <soapenv:Body>
             <wsaa:loginCms>
               <wsaa:in0>{cmsSigned}</wsaa:in0>
             </wsaa:loginCms>
           </soapenv:Body>
         </soapenv:Envelope>
         """;

    private static ArcaTicket ParseWsaaResponse(string xml, string cuit)
    {
        var doc = XDocument.Parse(xml);
        var taXml = doc.Descendants("loginCmsReturn").FirstOrDefault()?.Value
            ?? throw new InvalidOperationException("WSAA no devolvió loginCmsReturn.");

        var ta = XDocument.Parse(taXml);
        var token = ta.Descendants("token").First().Value;
        var sign = ta.Descendants("sign").First().Value;
        var genTime = DateTimeOffset.Parse(ta.Descendants("generationTime").First().Value);
        var expTime = DateTimeOffset.Parse(ta.Descendants("expirationTime").First().Value);

        return new ArcaTicket(token, sign, cuit, genTime, expTime);
    }

    // ── WSFEv1 ────────────────────────────────────────────────────────────────

    private static string BuildFecaeSolicitar(ArcaTicket ticket, ArcaInvoiceRequest req, int nextNumber)
    {
        var date = req.IssuedAt.ToString("yyyyMMdd");
        var cuitClean = ticket.Cuit.Replace("-", "");
        var receptorCuitClean = req.ReceptorCuit?.Replace("-", "") ?? "0";
        var docTipo = req.ReceptorCuit == null ? 99 : 80;

        return $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ar="http://ar.gov.afip.dif.FEV1/">
                  <soapenv:Header/>
                  <soapenv:Body>
                    <ar:FECAESolicitar>
                      <ar:Auth>
                        <ar:Token>{ticket.Token}</ar:Token>
                        <ar:Sign>{ticket.Sign}</ar:Sign>
                        <ar:Cuit>{cuitClean}</ar:Cuit>
                      </ar:Auth>
                      <ar:FeCAEReq>
                        <ar:FeCabReq>
                          <ar:CantReg>1</ar:CantReg>
                          <ar:PtoVta>{req.PointOfSaleNumber}</ar:PtoVta>
                          <ar:CbteTipo>{req.InvoiceTypeCode}</ar:CbteTipo>
                        </ar:FeCabReq>
                        <ar:FeDetReq>
                          <ar:FECAEDetRequest>
                            <ar:Concepto>{req.Concept}</ar:Concepto>
                            <ar:DocTipo>{docTipo}</ar:DocTipo>
                            <ar:DocNro>{receptorCuitClean}</ar:DocNro>
                            <ar:CbteDesde>{nextNumber}</ar:CbteDesde>
                            <ar:CbteHasta>{nextNumber}</ar:CbteHasta>
                            <ar:CbteFch>{date}</ar:CbteFch>
                            <ar:ImpTotal>{req.TotalAmount:F2}</ar:ImpTotal>
                            <ar:ImpTotConc>0.00</ar:ImpTotConc>
                            <ar:ImpNeto>{req.NetAmount:F2}</ar:ImpNeto>
                            <ar:ImpOpEx>0.00</ar:ImpOpEx>
                            <ar:ImpIVA>{req.VatAmount:F2}</ar:ImpIVA>
                            <ar:ImpTrib>0.00</ar:ImpTrib>
                            <ar:MonId>PES</ar:MonId>
                            <ar:MonCotiz>1</ar:MonCotiz>
                            <ar:CondicionIVAReceptorId>{req.ReceptorVatConditionId}</ar:CondicionIVAReceptorId>
                          </ar:FECAEDetRequest>
                        </ar:FeDetReq>
                      </ar:FeCAEReq>
                    </ar:FECAESolicitar>
                  </soapenv:Body>
                </soapenv:Envelope>
                """;
    }

    private static ArcaAuthorizeResult ParseFecaeResponse(string xml, int requestedNumber)
    {
        var doc = XDocument.Parse(xml);
        var det = doc.Descendants("FECAEDetResponse").FirstOrDefault();
        var result = det?.Element("Resultado")?.Value ?? "R";
        var cae = det?.Element("CAE")?.Value;
        var caeExpStr = det?.Element("CAEFchVto")?.Value; // yyyyMMdd

        if (result == "A" && cae != null && caeExpStr != null)
        {
            var caeExp = DateTimeOffset.ParseExact(caeExpStr, "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture);
            return new ArcaAuthorizeResult(true, requestedNumber, cae, caeExp, null, xml);
        }

        var obs = doc.Descendants("Obs")
            .Select(o => $"[{o.Element("Code")?.Value}] {o.Element("Msg")?.Value}")
            .ToList();
        var errs = doc.Descendants("Err")
            .Select(e => $"[{e.Element("Code")?.Value}] {e.Element("Msg")?.Value}")
            .ToList();

        var rejection = string.Join("; ", obs.Concat(errs).Where(s => !string.IsNullOrEmpty(s)));
        return new ArcaAuthorizeResult(false, requestedNumber, null, null,
            string.IsNullOrEmpty(rejection) ? "Rechazado por ARCA" : rejection, xml);
    }

    private static string BuildFECompUltimoAutorizado(ArcaTicket ticket, int pos, int cbteTipo) =>
        $"""
         <?xml version="1.0" encoding="UTF-8"?>
         <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ar="http://ar.gov.afip.dif.FEV1/">
           <soapenv:Header/>
           <soapenv:Body>
             <ar:FECompUltimoAutorizado>
               <ar:Auth>
                 <ar:Token>{ticket.Token}</ar:Token>
                 <ar:Sign>{ticket.Sign}</ar:Sign>
                 <ar:Cuit>{ticket.Cuit.Replace("-", "")}</ar:Cuit>
               </ar:Auth>
               <ar:PtoVta>{pos}</ar:PtoVta>
               <ar:CbteTipo>{cbteTipo}</ar:CbteTipo>
             </ar:FECompUltimoAutorizado>
           </soapenv:Body>
         </soapenv:Envelope>
         """;

    // ── HTTP ──────────────────────────────────────────────────────────────────

    private async Task<string> PostSoapAsync(string url, string soapBody, string action, CancellationToken ct)
    {
        using var content = new StringContent(soapBody, Encoding.UTF8, "text/xml");
        content.Headers.Add("SOAPAction", action);

        var response = await _http.PostAsync(url, content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "ARCA SOAP error {Status} action={Action}: {Body}",
                response.StatusCode, action, body[..Math.Min(body.Length, 500)]);
            throw new InvalidOperationException($"ARCA devolvió HTTP {(int)response.StatusCode} para {action}.");
        }

        return body;
    }
}
