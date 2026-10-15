using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Users;

namespace TallerControl.Application.Registration;

/// <summary>
/// Agrupa todas las entidades que se crean juntas al registrar un nuevo taller.
/// El repositorio los persiste en una transacción (REQ-01, D-22).
/// </summary>
public sealed record RegistrationAggregate(
    Tenant Tenant,
    TenantSettings Settings,
    Branch MainBranch,
    User Owner,
    Membership OwnerMembership,
    EmailVerificationToken VerificationToken);
