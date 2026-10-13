using TallerControl.Domain.Users;

namespace TallerControl.Application.Users;

/// <summary>Cuerpo de <c>POST /api/v1/invitations</c> (doc 05 §2, REQ-02).</summary>
public sealed record CreateInvitationRequest(
    string Email,
    string DisplayName,
    TenantRole Role,
    IReadOnlyList<string>? Permissions = null);

/// <summary>Respuesta 201 de <c>POST /api/v1/invitations</c>.</summary>
public sealed record InvitationDto(
    Guid Id,
    string Email,
    TenantRole Role,
    DateTimeOffset ExpiresAt);

/// <summary>Cuerpo de <c>POST /api/public/v1/invitations/{token}/accept</c> (doc 05 §2).</summary>
public sealed record AcceptInvitationRequest(string Password);

/// <summary>Cuerpo de <c>POST /api/v1/auth/forgot-password</c>.</summary>
public sealed record ForgotPasswordRequest(string Email);

/// <summary>Cuerpo de <c>POST /api/v1/auth/reset-password</c>.</summary>
public sealed record ResetPasswordRequest(string Token, string NewPassword);
