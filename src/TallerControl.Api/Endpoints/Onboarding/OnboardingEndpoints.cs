using TallerControl.Api.Identity;
using TallerControl.Application.Onboarding;
using TallerControl.Domain.Users;

namespace TallerControl.Api.Endpoints.Onboarding;

/// <summary>
/// Guía de inicio con progreso calculado (F-54).
/// GET /api/v1/onboarding → pasos y progreso (Owner solamente).
/// </summary>
internal static class OnboardingEndpoints
{
    public static IEndpointRouteBuilder MapOnboardingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/onboarding",
                (GetOnboardingHandler handler, CancellationToken ct) => GetAsync(handler, ct))
            .RequireAuthorization(policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new HasPermissionRequirement(Permission.SettingsManage));
            });

        return app;
    }

    private static async Task<IResult> GetAsync(GetOnboardingHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(ct);
        return Results.Ok(dto);
    }
}
