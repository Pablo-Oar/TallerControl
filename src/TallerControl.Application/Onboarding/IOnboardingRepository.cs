namespace TallerControl.Application.Onboarding;

public interface IOnboardingRepository
{
    Task<bool> HasCustomersAsync(CancellationToken ct);
    Task<bool> HasVehiclesAsync(CancellationToken ct);
    Task<bool> HasCatalogItemsAsync(CancellationToken ct);
    Task<bool> HasWorkOrdersAsync(CancellationToken ct);
    Task<bool> HasPaymentsAsync(CancellationToken ct);
    Task<bool> HasLogoAsync(CancellationToken ct);
}
