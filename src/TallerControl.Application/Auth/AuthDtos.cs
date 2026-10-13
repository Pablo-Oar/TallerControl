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
    string TimeZone);

public sealed record MeUserDto(Guid Id, string Email, string DisplayName);

public sealed record MeTenantDto(Guid Id, string Name);
