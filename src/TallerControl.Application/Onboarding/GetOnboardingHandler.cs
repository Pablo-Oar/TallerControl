namespace TallerControl.Application.Onboarding;

public sealed class GetOnboardingHandler
{
    private readonly IOnboardingRepository _repository;

    public GetOnboardingHandler(IOnboardingRepository repository)
    {
        _repository = repository;
    }

    public async Task<OnboardingDto> HandleAsync(CancellationToken ct)
    {
        // EF Core no es thread-safe: las queries deben ser secuenciales.
        var hasLogo = await _repository.HasLogoAsync(ct);
        var hasCustomers = await _repository.HasCustomersAsync(ct);
        var hasVehicles = await _repository.HasVehiclesAsync(ct);
        var hasCatalog = await _repository.HasCatalogItemsAsync(ct);
        var hasWorkOrders = await _repository.HasWorkOrdersAsync(ct);
        var hasPayments = await _repository.HasPaymentsAsync(ct);
        bool[] tasks = [hasLogo, hasCustomers, hasVehicles, hasCatalog, hasWorkOrders, hasPayments];

        var steps = new List<OnboardingStepDto>
        {
            new("logo",       "Subir el logo del taller",      tasks[0]),
            new("customers",  "Agregar el primer cliente",     tasks[1]),
            new("vehicles",   "Registrar el primer vehículo",  tasks[2]),
            new("catalog",    "Cargar el catálogo de servicios/productos", tasks[3]),
            new("workorders", "Crear la primera orden de trabajo", tasks[4]),
            new("payments",   "Registrar el primer cobro",     tasks[5]),
        };

        var completed = steps.Count(s => s.Completed);
        return new OnboardingDto(steps, completed, steps.Count, completed == steps.Count);
    }
}
