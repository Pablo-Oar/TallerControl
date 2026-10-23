using TallerControl.Domain.Users;

namespace TallerControl.Application.Auth;

/// <summary>Cuerpo de <c>POST /api/v1/auth/login</c> (doc 05 §2).</summary>
public sealed record LoginRequest(string Email, string Password, bool RememberMe = false);

/// <summary>Respuesta de <c>GET /api/v1/auth/me</c> (doc 05 §2).</summary>
public sealed record MeResponse(
    MeUserDto User,
    MeTenantDto Tenant,
    TenantRole Role,
    IReadOnlyList<string> Permissions,
    string TimeZone,
    /// <summary>Ruta inicial del rol: <c>/app</c> para Owner y AdminStaff, <c>/m</c> para Mechanic.</summary>
    string Home,
    /// <summary>Plan comercial del taller (D-17). "free" en el MVP.</summary>
    string PlanCode);

public sealed record MeUserDto(Guid Id, string Email, string DisplayName);

/// <summary><c>DefaultVatRate</c>: el IVA por defecto de la configuración del taller (precarga los renglones manuales y los ítems nuevos).</summary>
public sealed record MeTenantDto(Guid Id, string Name, decimal DefaultVatRate = 21m, int QuoteValidityDays = 15);
