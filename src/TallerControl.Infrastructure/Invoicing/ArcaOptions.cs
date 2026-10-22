namespace TallerControl.Infrastructure.Invoicing;

public sealed class ArcaOptions
{
    public const string SectionName = "Arca";

    /// <summary>URL de WSAA. Homologación: https://wsaahomo.afip.gov.ar/ws/services/LoginCms</summary>
    public string WsaaUrl { get; set; } = "https://wsaa.afip.gov.ar/ws/services/LoginCms";

    /// <summary>URL de WSFEv1. Homologación: https://wswhomo.afip.gov.ar/wsfev1/service.asmx</summary>
    public string WsfevUrl { get; set; } = "https://servicios1.afip.gov.ar/wsfev1/service.asmx";

    /// <summary>WSAA de homologación (certificados de prueba).</summary>
    public string HomologationWsaaUrl { get; set; } = "https://wsaahomo.afip.gov.ar/ws/services/LoginCms";

    /// <summary>WSFEv1 de homologación.</summary>
    public string HomologationWsfevUrl { get; set; } = "https://wswhomo.afip.gov.ar/wsfev1/service.asmx";

    /// <summary>Minutos de vigencia del ticket de autorización antes de renovar (WSAA da ~12 h).</summary>
    public int TicketCacheDurationMinutes { get; set; } = 600;

    /// <summary>Service ID para WSAA (siempre "wsfe" para WSFEv1).</summary>
    public string WsId { get; set; } = "wsfe";
}
