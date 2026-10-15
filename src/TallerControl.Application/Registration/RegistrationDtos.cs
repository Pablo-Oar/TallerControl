namespace TallerControl.Application.Registration;

/// <summary>Cuerpo de <c>POST /api/public/v1/register</c>.</summary>
public sealed record RegisterTenantRequest(
    string WorkshopName,
    string DisplayName,
    string Email,
    string Password);

/// <summary>Respuesta de <c>POST /api/public/v1/register</c>.</summary>
public sealed record RegisterTenantResponse(string Email);

/// <summary>Respuesta de <c>GET /api/public/v1/verify-email</c>.</summary>
public sealed record VerifyEmailResponse(bool Success, string Message);
